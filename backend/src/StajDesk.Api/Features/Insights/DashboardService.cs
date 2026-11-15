using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public class DashboardService(AppDbContext db, ITicketService tickets, LeaveService leave, CollaborationService collab, DirectoryService directory,
    ICacheService cache, TimeProvider clock)
{
    public Task<DashboardDto> GetAsync(UserContext me, CancellationToken ct) =>
        cache.GetOrSetAsync(CacheKeys.Dashboard(me.Id), () => BuildAsync(me, ct), TimeSpan.FromSeconds(30));

    private async Task<DashboardDto> BuildAsync(UserContext me, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var u = await db.Users.AsNoTracking().Include(x => x.JobTitle).Include(x => x.Department).Include(x => x.Manager).FirstAsync(x => x.Id == me.Id, ct);
        var profile = new DashboardProfile(u.FullName, u.JobTitle?.Name, u.Department?.Name, u.Manager?.FullName, u.HireDate,
            u.HireDate is { } h ? Math.Round((today.DayNumber - h.DayNumber) / 365.25, 1) : 0, me.Roles.ToList());

        var myOpen = await db.Tickets.CountAsync(t => t.RequesterId == me.Id && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed, ct);
        var awaiting = await db.Tickets.CountAsync(t => t.RequesterId == me.Id && t.Status == TicketStatus.Resolved, ct);
        var myPending = await db.FormSubmissions.CountAsync(s => s.SubmitterId == me.Id && s.Status == SubmissionStatus.Pending, ct);
        var approvals = await db.FormSubmissions.CountAsync(s => s.Status == SubmissionStatus.Pending && s.PendingApproverIds.Contains(me.Id), ct);
        var toComplete = await db.FormSubmissions.CountAsync(s => s.Status == SubmissionStatus.Approved && s.AssigneeId == me.Id, ct);
        var tasks = (await collab.TasksAsync(new TaskQuery { Scope = "mine", PageSize = 6 }, me, ct)).Items.Where(t => t.Status != WorkTaskStatus.Done).ToList();
        var visible = await tickets.VisibilityAsync(me, ct);
        var recent = await db.Tickets.AsNoTracking().Where(visible).Where(t => me.Has(Permissions.TicketsManage) || t.RequesterId == me.Id)
            .OrderByDescending(t => t.UpdatedAt).Take(6).Select(TicketService.Projection(now)).ToListAsync(ct);

        SupportWidget? support = null;
        if (me.Has(Permissions.TicketsManage))
        {
            var q = db.Tickets.Where(visible).Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed);
            var groups = await db.GroupMembers.Where(g => g.UserId == me.Id).Select(g => (int?)g.GroupId).ToListAsync(ct);
            support = new SupportWidget(await q.CountAsync(ct), await q.CountAsync(t => t.AssigneeId == null, ct), await q.CountAsync(t => t.DueAt < now, ct),
                await q.CountAsync(t => t.AssigneeId == me.Id, ct), await q.CountAsync(t => groups.Contains(t.GroupId), ct));
        }

        TicketStatsWidget? stats = null;
        if (me.Has(Permissions.ReportsView) || me.Has(Permissions.ReportsViewDepartment) || me.Has(Permissions.TicketsViewAll))
        {
            var q = db.Tickets.AsNoTracking().Where(visible);
            var label = me.Has(Permissions.TicketsViewAll) || me.Has(Permissions.ReportsView) ? "Şirket geneli" : "Sorumlu olduğunuz birimler";
            var byStatus = await q.GroupBy(t => t.Status).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
            var from = now.Date.AddDays(-13);
            var created = await q.Where(t => t.CreatedAt >= from).Select(t => t.CreatedAt).ToListAsync(ct);
            var resolved = await q.Where(t => t.ResolvedAt != null).Select(t => new { t.CreatedAt, R = t.ResolvedAt!.Value, t.DueAt }).ToListAsync(ct);
            stats = new TicketStatsWidget(label,
                Enum.GetValues<TicketStatus>().Select(s => new Count(s.ToString(), TicketRules.Label(s), byStatus.FirstOrDefault(x => x.Key == s)?.C ?? 0)).ToList(),
                Enumerable.Range(0, 14).Select(i => from.AddDays(i)).Select(d => new TrendPoint(DateOnly.FromDateTime(d), created.Count(c => c.Date == d), resolved.Count(r => r.R.Date == d))).ToList(),
                resolved.Count == 0 ? 100 : Math.Round(resolved.Count(r => r.R <= r.DueAt) * 100.0 / resolved.Count, 1),
                resolved.Count == 0 ? 0 : Math.Round(resolved.Average(r => (r.R - r.CreatedAt).TotalHours), 1));
        }

        TeamWidget? team = null;
        var reports = await db.Users.Where(x => x.ManagerId == me.Id && x.IsActive).Select(x => x.Id).ToListAsync(ct);
        if (reports.Count > 0)
        {
            var leaves = await leave.LeaveCalendarAsync(me with { DepartmentId = me.DepartmentId }, today, today, ct);
            var onLeave = leaves.Where(l => reports.Contains(l.User.Id) && l.Status != SubmissionStatus.Pending).Select(l => l.User).DistinctBy(x => x.Id).ToList();
            team = new TeamWidget(reports.Count, onLeave.Count, approvals, onLeave);
        }

        InventoryWidget? inventory = null;
        var myAssets = await db.Assets.CountAsync(a => a.AssignedToId == me.Id, ct);
        if (me.Has(Permissions.InventoryView))
        {
            var status = await db.Assets.GroupBy(a => a.Status).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
            int S(AssetStatus s) => status.FirstOrDefault(x => x.Key == s)?.C ?? 0;
            inventory = new InventoryWidget(status.Sum(x => x.C), S(AssetStatus.InUse), S(AssetStatus.InStock), S(AssetStatus.InRepair),
                await db.Assets.CountAsync(a => a.WarrantyEndsAt != null && a.WarrantyEndsAt >= today && a.WarrantyEndsAt <= today.AddDays(90), ct),
                await db.SoftwareLicenses.CountAsync(l => l.ExpiresAt != null && l.ExpiresAt <= today.AddDays(60), ct), myAssets);
        }

        AdminWidget? admin = null;
        if (me.Has(Permissions.UsersManage) || me.Has(Permissions.AccountsApprove))
            admin = new AdminWidget(await db.AccountRequests.CountAsync(r => r.Status == AccountRequestStatus.Pending, ct),
                await db.Users.CountAsync(x => x.IsActive && x.MustChangePassword, ct), await db.Users.CountAsync(x => !x.IsActive, ct), await db.Users.CountAsync(x => x.IsActive, ct));

        return new DashboardDto(profile, await leave.MyLeaveAsync(me.Id, ct), myOpen, awaiting, myPending, approvals, toComplete,
            await collab.UnreadNotificationsAsync(me.Id, ct), await collab.UnreadMessagesAsync(me.Id, ct), tasks,
            await collab.AnnouncementsAsync(me, null, false, 4, ct), (await collab.EventsAsync(me, true, ct)).Take(4).ToList(),
            await directory.BirthdaysAsync(7, ct), await collab.AchievementsAsync(4, ct), await collab.OnCallAsync(true, ct),
            recent, support, stats, team, inventory, admin, now);
    }
}
