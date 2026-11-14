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

// ============================================================ DTO'lar
public record Count(string Key, string Label, int Value, string? Color = null);
public record TrendPoint(DateOnly Date, int Created, int Resolved);

public record DashboardProfile(string FullName, string? JobTitle, string? Department, string? Manager, DateOnly? HireDate, double YearsOfService, IReadOnlyList<string> Roles);
public record SupportWidget(int QueueOpen, int Unassigned, int Overdue, int AssignedToMe, int MyGroups);
public record TicketStatsWidget(string Scope, IReadOnlyList<Count> ByStatus, IReadOnlyList<TrendPoint> Trend, double SlaCompliance, double AvgResolutionHours);
public record TeamWidget(int TeamSize, int OnLeaveToday, int PendingApprovals, IReadOnlyList<UserRef> OnLeave);
public record InventoryWidget(int Total, int InUse, int InStock, int InRepair, int WarrantyExpiring, int LicensesExpiring, int MyAssets);
public record AdminWidget(int PendingAccountRequests, int MustChangePassword, int InactiveUsers, int ActiveUsers);
public record DashboardDto(DashboardProfile Profile, LeaveBalanceDto Leave, int MyOpenTickets, int MyTicketsAwaitingConfirmation, int MyPendingRequests, int ApprovalsPending,
    int AssignedToComplete, int UnreadNotifications, int UnreadMessages, IReadOnlyList<TaskDto> Tasks, IReadOnlyList<AnnouncementDto> Announcements,
    IReadOnlyList<EventDto> Events, IReadOnlyList<BirthdayEntry> Birthdays, IReadOnlyList<AchievementDto> Achievements, IReadOnlyList<OnCallDto> OnCall,
    IReadOnlyList<TicketListItem> RecentTickets, SupportWidget? Support, TicketStatsWidget? TicketStats, TeamWidget? Team, InventoryWidget? Inventory, AdminWidget? Admin, DateTime GeneratedAt);

public class ReportFilter
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? DepartmentId { get; set; }
    public int? CategoryId { get; set; }
    public TicketStatus? Status { get; set; }
    public string? Module { get; set; }
}

public record AgentRow(Guid Id, string FullName, int Assigned, int Resolved, double AvgResolutionHours, double SlaCompliance);
public record TicketReport(string Scope, DateOnly From, DateOnly To, int Total, int Open, int Resolved, int Overdue, double AvgResolutionHours, double SlaCompliance,
    IReadOnlyList<Count> ByStatus, IReadOnlyList<Count> ByPriority, IReadOnlyList<Count> ByCategory, IReadOnlyList<Count> ByDepartment, IReadOnlyList<Count> ByGroup,
    IReadOnlyList<TrendPoint> Trend, IReadOnlyList<Count> Aging, IReadOnlyList<AgentRow> Agents);
public record FormRow(string Key, string Name, string Module, int Total, int Pending, int Approved, int Rejected, int Completed, double AvgDecisionHours);
public record WorkflowReport(string Scope, DateOnly From, DateOnly To, int Total, int Pending, int Approved, int Rejected, int Completed, double AvgDecisionHours,
    IReadOnlyList<Count> ByModule, IReadOnlyList<FormRow> ByForm, IReadOnlyList<Count> ByDepartment, IReadOnlyList<TrendPoint> Trend);
public record LicenseUsage(string Name, int Seats, int Used, DateOnly? ExpiresAt);
public record InventoryReport(int Total, decimal TotalValue, IReadOnlyList<Count> ByType, IReadOnlyList<Count> ByStatus, IReadOnlyList<Count> ByDepartment,
    IReadOnlyList<AssetDto> WarrantyExpiring, IReadOnlyList<LicenseUsage> Licenses);
public record PeopleReport(int Active, int Inactive, IReadOnlyList<Count> ByDepartmentType, IReadOnlyList<Count> ByDepartment, IReadOnlyList<Count> ByEmploymentType, int NewHires90, int MustChangePassword);

public record PublicStats(int Employees, int Stores, int Cities, int HeadOfficeDepartments, int ResolvedRequestsThisYear);
public record PublicHome(IReadOnlyList<BannerDto> Banners, IReadOnlyList<AnnouncementDto> News, IReadOnlyList<JobDto> Jobs, IReadOnlyList<EventDto> Events, PublicStats Stats);
public record Rate(string Code, string Name, decimal Value, string Source, string AsOf);
public record MarketData(IReadOnlyList<Rate> Rates, DateTime FetchedAt, IReadOnlyList<string> Errors);
public record WeatherDay(DateOnly Date, double Min, double Max, int Code, string Description);
public record WeatherData(string City, double Temperature, double? Humidity, double? Wind, int Code, string Description, IReadOnlyList<WeatherDay> Days, string Source, DateTime FetchedAt);

public record ComponentHealth(string Name, string Role, bool Healthy, long LatencyMs, string Detail);
public record SystemHealth(bool Healthy, DateTime CheckedAt, IReadOnlyList<ComponentHealth> Components, int OutboxPending, int OutboxFailed);
public record CacheEntry(string Key, double? TtlSeconds);

public record AppEntry(string Key, string Name, string Module, string Icon, string Color, string Route, string Kind, string? Description, int Badge);

// ============================================================ Dashboard
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

// ============================================================ Raporlar
public class ReportService(AppDbContext db, AccessScope scope, TimeProvider clock)
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private (DateTime From, DateTime To, DateOnly F, DateOnly T) Range(ReportFilter f)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var to = f.To ?? today;
        var from = f.From ?? to.AddDays(-29);
        if (from > to) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 366) from = to.AddDays(-366);
        return (from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc), from, to);
    }

    /// <summary>Rapor kapsamı: şirket geneli rapor yetkisi veya tüm talep yetkisi yoksa yalnızca sorumlu olunan departmanlar.</summary>
    private async Task<(Expression<Func<Ticket, bool>> Filter, string Label)> TicketScopeAsync(UserContext me, CancellationToken ct)
    {
        if (me.Has(Permissions.ReportsView) || me.Has(Permissions.TicketsViewAll)) return (_ => true, "Şirket geneli");
        if (!me.Has(Permissions.ReportsViewDepartment)) throw AppException.Forbidden();
        var depts = (await scope.DepartmentIdsAsync(me, ct)).ToList();
        return (t => t.DepartmentId != null && depts.Contains(t.DepartmentId.Value), "Sorumlu olduğunuz departmanlar");
    }

    private IQueryable<Ticket> Filtered(IQueryable<Ticket> q, ReportFilter f, DateTime from, DateTime to)
    {
        q = q.Where(t => t.CreatedAt >= from && t.CreatedAt <= to);
        if (f.DepartmentId is { } d) q = q.Where(t => t.DepartmentId == d);
        if (f.CategoryId is { } c) q = q.Where(t => t.CategoryId == c);
        if (f.Status is { } s) q = q.Where(t => t.Status == s);
        return q;
    }

    public async Task<TicketReport> TicketsAsync(ReportFilter f, UserContext me, CancellationToken ct)
    {
        var (from, to, F, T) = Range(f);
        var (filter, label) = await TicketScopeAsync(me, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await Filtered(db.Tickets.AsNoTracking().Where(filter), f, from, to).Select(t => new
        {
            t.Status, t.Priority, Cat = t.Category.Name, CatColor = t.Category.Color, Dept = t.Department == null ? "Belirtilmemiş" : t.Department.Name,
            Group = t.Group == null ? "Atanmamış" : t.Group.Name, t.CreatedAt, t.ResolvedAt, t.DueAt, t.AssigneeId,
            Agent = t.Assignee == null ? null : t.Assignee.FirstName + " " + t.Assignee.LastName,
        }).ToListAsync(ct);

        var resolved = rows.Where(r => r.ResolvedAt != null).ToList();
        var active = rows.Where(r => r.Status is not (TicketStatus.Resolved or TicketStatus.Closed)).ToList();
        double Avg(IEnumerable<double> x) => x.Any() ? Math.Round(x.Average(), 1) : 0;
        double Sla(IReadOnlyCollection<dynamic> x) => x.Count == 0 ? 100 : Math.Round(x.Count(r => r.ResolvedAt <= r.DueAt) * 100.0 / x.Count, 1);

        var days = Enumerable.Range(0, T.DayNumber - F.DayNumber + 1).Select(i => F.AddDays(i)).ToList();
        var aging = new (string Key, string Label, Func<double, bool> Match)[]
        {
            ("0-1", "1 günden az", d => d < 1), ("1-3", "1-3 gün", d => d is >= 1 and < 3), ("3-7", "3-7 gün", d => d is >= 3 and < 7), ("7+", "7 günden fazla", d => d >= 7),
        };
        return new TicketReport(label, F, T, rows.Count, active.Count, resolved.Count, active.Count(r => r.DueAt < now),
            Avg(resolved.Select(r => (r.ResolvedAt!.Value - r.CreatedAt).TotalHours)), Sla(resolved.Cast<dynamic>().ToList()),
            Enum.GetValues<TicketStatus>().Select(s => new Count(s.ToString(), TicketRules.Label(s), rows.Count(r => r.Status == s))).ToList(),
            Enum.GetValues<TicketPriority>().Select(p => new Count(p.ToString(), TicketRules.Label(p), rows.Count(r => r.Priority == p))).ToList(),
            rows.GroupBy(r => (r.Cat, r.CatColor)).Select(g => new Count(g.Key.Cat, g.Key.Cat, g.Count(), g.Key.CatColor)).OrderByDescending(c => c.Value).ToList(),
            rows.GroupBy(r => r.Dept).Select(g => new Count(g.Key, g.Key, g.Count())).OrderByDescending(c => c.Value).Take(15).ToList(),
            rows.GroupBy(r => r.Group).Select(g => new Count(g.Key, g.Key, g.Count())).OrderByDescending(c => c.Value).ToList(),
            days.Select(d => new TrendPoint(d, rows.Count(r => DateOnly.FromDateTime(r.CreatedAt) == d), resolved.Count(r => DateOnly.FromDateTime(r.ResolvedAt!.Value) == d))).ToList(),
            aging.Select(a => new Count(a.Key, a.Label, active.Count(r => a.Match((now - r.CreatedAt).TotalDays)))).ToList(),
            rows.Where(r => r.AssigneeId != null).GroupBy(r => (r.AssigneeId!.Value, r.Agent!)).Select(g =>
            {
                var res = g.Where(r => r.ResolvedAt != null).ToList();
                return new AgentRow(g.Key.Item1, g.Key.Item2, g.Count(), res.Count, Avg(res.Select(r => (r.ResolvedAt!.Value - r.CreatedAt).TotalHours)), Sla(res.Cast<dynamic>().ToList()));
            }).OrderByDescending(a => a.Resolved).ToList());
    }

    public async Task<byte[]> TicketsCsvAsync(ReportFilter f, UserContext me, CancellationToken ct)
    {
        var (from, to, _, _) = Range(f);
        var (filter, _) = await TicketScopeAsync(me, ct);
        var rows = await Filtered(db.Tickets.AsNoTracking().Where(filter), f, from, to).OrderBy(t => t.Id).Select(t => new
        {
            t.Id, t.Title, t.Status, t.Priority, Cat = t.Category.Name, Dept = t.Department == null ? "" : t.Department.Name,
            Requester = t.Requester.FirstName + " " + t.Requester.LastName, Group = t.Group == null ? "" : t.Group.Name,
            Assignee = t.Assignee == null ? "" : t.Assignee.FirstName + " " + t.Assignee.LastName, t.CreatedAt, t.DueAt, t.ResolvedAt,
        }).Take(20000).ToListAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("No;Başlık;Durum;Öncelik;Kategori;Departman;Talep Sahibi;Grup;Atanan;Oluşturma;SLA Hedefi;Çözüm");
        static string Esc(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        foreach (var r in rows)
            sb.AppendLine(string.Join(';', r.Id, Esc(r.Title), TicketRules.Label(r.Status), TicketRules.Label(r.Priority), Esc(r.Cat), Esc(r.Dept), Esc(r.Requester), Esc(r.Group), Esc(r.Assignee),
                r.CreatedAt.ToLocalTime().ToString("g", Tr), r.DueAt.ToLocalTime().ToString("g", Tr), r.ResolvedAt?.ToLocalTime().ToString("g", Tr) ?? ""));
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    public async Task<WorkflowReport> WorkflowsAsync(ReportFilter f, UserContext me, CancellationToken ct)
    {
        var (from, to, F, T) = Range(f);
        var q = db.FormSubmissions.AsNoTracking().Where(s => s.CreatedAt >= from && s.CreatedAt <= to);
        string label;
        if (me.Has(Permissions.ReportsView) || me.Has(Permissions.SubmissionsViewAll)) label = "Şirket geneli";
        else if (me.Has(Permissions.ReportsViewDepartment))
        {
            var depts = (await scope.DepartmentIdsAsync(me, ct)).ToList();
            q = q.Where(s => (s.DepartmentId != null && depts.Contains(s.DepartmentId.Value)) || (s.FormDefinition.OwnerDepartmentId != null && depts.Contains(s.FormDefinition.OwnerDepartmentId.Value)));
            label = "Sorumlu olduğunuz departmanlar";
        }
        else throw AppException.Forbidden();
        if (f.DepartmentId is { } d) q = q.Where(s => s.DepartmentId == d);
        if (!string.IsNullOrWhiteSpace(f.Module)) q = q.Where(s => s.FormDefinition.Module == f.Module);

        var rows = await q.Select(s => new
        {
            s.Status, s.FormDefinition.Key, s.FormDefinition.Name, s.FormDefinition.Module, Dept = s.Department == null ? "Belirtilmemiş" : s.Department.Name, s.CreatedAt,
            Decided = s.Actions.Where(a => a.Action == SubmissionActionType.Approved || a.Action == SubmissionActionType.Rejected).Max(a => (DateTime?)a.CreatedAt),
            Finished = s.Actions.Where(a => a.Action == SubmissionActionType.Completed || a.Action == SubmissionActionType.Rejected || a.Action == SubmissionActionType.Approved).Max(a => (DateTime?)a.CreatedAt),
        }).ToListAsync(ct);
        double AvgHours(IEnumerable<dynamic> x)
        {
            var list = x.Where(r => r.Decided != null).Select(r => ((DateTime)r.Decided - (DateTime)r.CreatedAt).TotalHours).ToList();
            return list.Count == 0 ? 0 : Math.Round(list.Average(), 1);
        }
        int C(SubmissionStatus s) => rows.Count(r => r.Status == s);
        var days = Enumerable.Range(0, T.DayNumber - F.DayNumber + 1).Select(i => F.AddDays(i)).ToList();
        return new WorkflowReport(label, F, T, rows.Count, C(SubmissionStatus.Pending), C(SubmissionStatus.Approved), C(SubmissionStatus.Rejected), C(SubmissionStatus.Completed),
            AvgHours(rows.Cast<dynamic>()),
            rows.GroupBy(r => r.Module).Select(g => new Count(g.Key, g.Key, g.Count())).OrderByDescending(x => x.Value).ToList(),
            rows.GroupBy(r => (r.Key, r.Name, r.Module)).Select(g => new FormRow(g.Key.Key, g.Key.Name, g.Key.Module, g.Count(), g.Count(r => r.Status == SubmissionStatus.Pending),
                g.Count(r => r.Status == SubmissionStatus.Approved), g.Count(r => r.Status == SubmissionStatus.Rejected), g.Count(r => r.Status == SubmissionStatus.Completed),
                AvgHours(g.Cast<dynamic>()))).OrderByDescending(x => x.Total).ToList(),
            rows.GroupBy(r => r.Dept).Select(g => new Count(g.Key, g.Key, g.Count())).OrderByDescending(x => x.Value).Take(15).ToList(),
            days.Select(day => new TrendPoint(day, rows.Count(r => DateOnly.FromDateTime(r.CreatedAt) == day),
                rows.Count(r => r.Finished is { } fin && DateOnly.FromDateTime(fin) == day && r.Status != SubmissionStatus.Pending))).ToList());
    }

    public async Task<InventoryReport> InventoryAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var rows = await db.Assets.AsNoTracking().Select(a => new { a.Type, a.Status, Dept = a.Department == null ? "Belirtilmemiş" : a.Department.Name, a.PurchaseCost }).ToListAsync(ct);
        var expiring = await db.Assets.AsNoTracking().Where(a => a.WarrantyEndsAt != null && a.WarrantyEndsAt >= today && a.WarrantyEndsAt <= today.AddDays(90))
            .OrderBy(a => a.WarrantyEndsAt).Take(20).Select(a => new AssetDto(a.Id, a.Name, a.AssetTag, a.Type, a.Status, a.Manufacturer, a.Model, a.SerialNumber,
                a.Department == null ? null : new DeptRef(a.Department.Id, a.Department.Name), a.Location,
                a.AssignedTo == null ? null : new UserRef(a.AssignedTo.Id, a.AssignedTo.FirstName + " " + a.AssignedTo.LastName, a.AssignedTo.Email),
                a.PurchaseDate, a.WarrantyEndsAt, a.PurchaseCost, a.Notes, a.CreatedAt, a.UpdatedAt, 0)).ToListAsync(ct);
        var licenses = await db.SoftwareLicenses.AsNoTracking().OrderBy(l => l.Name).Select(l => new LicenseUsage(l.Name, l.Seats, l.Installations.Count, l.ExpiresAt)).ToListAsync(ct);
        return new InventoryReport(rows.Count, rows.Where(r => r.Status != AssetStatus.Retired).Sum(r => r.PurchaseCost ?? 0),
            rows.GroupBy(r => r.Type).Select(g => new Count(g.Key.ToString(), g.Key.ToString(), g.Count())).OrderByDescending(x => x.Value).ToList(),
            rows.GroupBy(r => r.Status).Select(g => new Count(g.Key.ToString(), g.Key.ToString(), g.Count())).ToList(),
            rows.GroupBy(r => r.Dept).Select(g => new Count(g.Key, g.Key, g.Count())).OrderByDescending(x => x.Value).Take(15).ToList(), expiring, licenses);
    }

    public async Task<PeopleReport> PeopleAsync(CancellationToken ct)
    {
        var since = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddDays(-90));
        var rows = await db.Users.AsNoTracking().Select(u => new { u.IsActive, u.EmploymentType, u.HireDate, u.MustChangePassword, Dept = u.Department == null ? "Belirtilmemiş" : u.Department.Name, Type = u.Department == null ? (DepartmentType?)null : u.Department.Type }).ToListAsync(ct);
        var active = rows.Where(r => r.IsActive).ToList();
        return new PeopleReport(active.Count, rows.Count - active.Count,
            active.GroupBy(r => r.Type).Select(g => new Count(g.Key?.ToString() ?? "None", g.Key switch { DepartmentType.HeadOffice => "Genel Müdürlük", DepartmentType.Store => "Mağazalar", DepartmentType.Region => "Bölge Yönetimi", _ => "Belirtilmemiş" }, g.Count())).ToList(),
            active.GroupBy(r => r.Dept).Select(g => new Count(g.Key, g.Key, g.Count())).OrderByDescending(x => x.Value).Take(15).ToList(),
            active.GroupBy(r => r.EmploymentType).Select(g => new Count(g.Key.ToString(), g.Key switch { EmploymentType.FullTime => "Tam zamanlı", EmploymentType.PartTime => "Yarı zamanlı", _ => "Stajyer" }, g.Count())).ToList(),
            active.Count(r => r.HireDate >= since), active.Count(r => r.MustChangePassword));
    }
}

// ============================================================ Genel site ve dış veriler
public class PublicSiteService(AppDbContext db, CollaborationService collab, ICacheService cache, IHttpClientFactory http, TimeProvider clock, ILogger<PublicSiteService> logger)
{
    private static readonly Dictionary<string, (string Name, double Lat, double Lon)> Cities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["istanbul"] = ("İstanbul", 41.0082, 28.9784), ["ankara"] = ("Ankara", 39.9334, 32.8597), ["izmir"] = ("İzmir", 38.4237, 27.1428), ["adana"] = ("Adana", 37.0000, 35.3213),
    };

    /// <summary>Genel tanıtım sitesi verisi. Yalnızca herkese açık işaretlenmiş içerik ve toplu istatistikler döner; çalışan kişisel bilgisi içermez.</summary>
    public Task<PublicHome> HomeAsync(CancellationToken ct) => cache.GetOrSetAsync(CacheKeys.PublicHome, async () =>
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var banners = (await collab.BannersAsync(ct)).Where(b => b.IsLive).ToList();
        var news = await db.Announcements.AsNoTracking().Where(a => a.IsPublic && a.PublishAt <= now && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.PublishAt).Take(6).Select(a => new AnnouncementDto(a.Id, a.Title, a.Summary, a.Body, a.Kind, true, a.IsPinned, a.PublishAt, a.ExpiresAt, null,
                new UserRef(Guid.Empty, "Kurumsal İletişim", ""))).ToListAsync(ct);
        var jobs = await collab.JobsAsync(true, true, ct);
        var events = await db.Events.AsNoTracking().Where(e => e.IsPublic && e.StartsAt >= now.AddHours(-3)).OrderBy(e => e.StartsAt).Take(4)
            .Select(e => new EventDto(e.Id, e.Title, e.Description, e.Location, e.Category, e.StartsAt, e.EndsAt, true, 0, false, new UserRef(Guid.Empty, "", ""))).ToListAsync(ct);
        var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var stats = new PublicStats(await db.Users.CountAsync(u => u.IsActive, ct), await db.Stores.CountAsync(s => s.Status != StoreStatus.Closed, ct),
            await db.Stores.Where(s => s.City != null).Select(s => s.City).Distinct().CountAsync(ct),
            await db.Departments.CountAsync(d => d.Type == DepartmentType.HeadOffice && d.IsActive, ct),
            await db.Tickets.CountAsync(t => t.ResolvedAt >= yearStart, ct) + await db.FormSubmissions.CountAsync(s => s.Status == SubmissionStatus.Completed && s.CompletedAt >= yearStart, ct));
        return new PublicHome(banners, news, jobs, events, stats);
    }, TimeSpan.FromMinutes(2));

    /// <summary>USD/EUR: TCMB gösterge kurları (döviz satış). Gram altın: Truncgil Finans piyasa verisi; erişilemezse gold-api.com XAU/USD × TCMB USD / 31,1035.</summary>
    public Task<MarketData> MarketAsync(CancellationToken ct) => cache.GetOrSetAsync(CacheKeys.Market, async () =>
    {
        var rates = new List<Rate>();
        var errors = new List<string>();
        var client = http.CreateClient("external");
        decimal? usd = null;
        try
        {
            var xml = XDocument.Parse(await client.GetStringAsync("https://www.tcmb.gov.tr/kurlar/today.xml", ct));
            var date = xml.Root?.Attribute("Tarih")?.Value ?? "";
            foreach (var (code, name) in new[] { ("USD", "ABD Doları"), ("EUR", "Euro") })
            {
                var node = xml.Root?.Elements("Currency").FirstOrDefault(e => e.Attribute("Kod")?.Value == code);
                if (decimal.TryParse(node?.Element("ForexSelling")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                {
                    rates.Add(new Rate(code, name, v, "TCMB gösterge kuru (döviz satış)", date));
                    if (code == "USD") usd = v;
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("TCMB kurları alınamadı: {Error}", ex.Message);
            errors.Add("Döviz kurları şu anda alınamıyor.");
        }
        // Gram altın: önce doğrudan piyasa verisi (Truncgil Finans, gram altın satış). Erişilemezse
        // gold-api.com ons fiyatı × TCMB USD ile hesaplanır. İkisi de yoksa değer gösterilmez.
        try
        {
            using var doc = JsonDocument.Parse(await client.GetStringAsync("https://finans.truncgil.com/today.json", ct));
            var raw = doc.RootElement.GetProperty("gram-altin").GetProperty("Satış").GetString() ?? "";
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out var price) || price <= 0)
                throw new FormatException("Gram altın fiyatı okunamadı.");
            var updated = doc.RootElement.TryGetProperty("Update_Date", out var u) ? u.GetString() ?? "" : "";
            rates.Add(new Rate("GRAM", "Gram Altın", price, "Truncgil Finans (gram altın satış)", updated));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Gram altın (Truncgil) alınamadı, hesaplanan değere geçiliyor: {Error}", ex.Message);
            try
            {
                using var doc = JsonDocument.Parse(await client.GetStringAsync("https://api.gold-api.com/price/XAU", ct));
                var ounceUsd = doc.RootElement.GetProperty("price").GetDecimal();
                var updated = doc.RootElement.TryGetProperty("updatedAt", out var u) ? u.GetString() ?? "" : "";
                if (usd is { } usdTry)
                    rates.Add(new Rate("GRAM", "Gram Altın", Math.Round(ounceUsd * usdTry / 31.1034768m, 2), "gold-api.com XAU/USD × TCMB USD (hesaplanan)", updated));
                else errors.Add("Gram altın hesaplanamadı (USD kuru alınamadı).");
            }
            catch (Exception ex2) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Altın fiyatı alınamadı: {Error}", ex2.Message);
                errors.Add("Altın fiyatı şu anda alınamıyor.");
            }
        }
        return new MarketData(rates, clock.GetUtcNow().UtcDateTime, errors);
    }, m => m.Errors.Count > 0 ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(15)); // kaynak hatası 15 dk boyunca önbellekte kalmasın


    public async Task<WeatherData?> WeatherAsync(string city, CancellationToken ct)
    {
        if (!Cities.TryGetValue(city, out var c)) throw AppException.BadRequest("Desteklenen şehirler: İstanbul, Ankara, İzmir, Adana.");
        try
        {
            return await cache.GetOrSetAsync($"{CacheKeys.Weather}:{city.ToLowerInvariant()}", async () =>
            {
                var url = $"https://api.open-meteo.com/v1/forecast?latitude={c.Lat.ToString(CultureInfo.InvariantCulture)}&longitude={c.Lon.ToString(CultureInfo.InvariantCulture)}" +
                          "&current=temperature_2m,relative_humidity_2m,wind_speed_10m,weather_code&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=Europe%2FIstanbul&forecast_days=4";
                using var doc = JsonDocument.Parse(await http.CreateClient("external").GetStringAsync(url, ct));
                var cur = doc.RootElement.GetProperty("current");
                var daily = doc.RootElement.GetProperty("daily");
                var dates = daily.GetProperty("time").EnumerateArray().Select(x => DateOnly.Parse(x.GetString()!)).ToList();
                var codes = daily.GetProperty("weather_code").EnumerateArray().Select(x => x.GetInt32()).ToList();
                var max = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(x => x.GetDouble()).ToList();
                var min = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(x => x.GetDouble()).ToList();
                var code = cur.GetProperty("weather_code").GetInt32();
                return new WeatherData(c.Name, cur.GetProperty("temperature_2m").GetDouble(), cur.GetProperty("relative_humidity_2m").GetDouble(), cur.GetProperty("wind_speed_10m").GetDouble(),
                    code, Describe(code), dates.Select((d, i) => new WeatherDay(d, min[i], max[i], codes[i], Describe(codes[i]))).ToList(), "Open-Meteo", clock.GetUtcNow().UtcDateTime);
            }, TimeSpan.FromMinutes(20));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            logger.LogWarning("Hava durumu alınamadı: {Error}", ex.Message);
            return null;
        }
    }

    private static string Describe(int code) => code switch
    {
        0 => "Açık", 1 => "Çoğunlukla açık", 2 => "Parçalı bulutlu", 3 => "Kapalı", 45 or 48 => "Sisli",
        51 or 53 or 55 => "Çiseleme", 56 or 57 => "Donan çiseleme", 61 => "Hafif yağmurlu", 63 => "Yağmurlu", 65 => "Kuvvetli yağmur",
        66 or 67 => "Donan yağmur", 71 => "Hafif kar", 73 => "Kar yağışlı", 75 => "Yoğun kar", 77 => "Kar taneleri",
        80 or 81 or 82 => "Sağanak", 85 or 86 => "Kar sağanağı", 95 => "Gök gürültülü", 96 or 99 => "Dolu ile fırtına", _ => "Bilinmiyor",
    };
}

// ============================================================ Sistem durumu
public class SystemService(AppDbContext db, IConnectionMultiplexer redis, RabbitMqPublisher rabbit, ICacheService cache, IHttpClientFactory http, IConfiguration config)
{
    public async Task<SystemHealth> HealthAsync(CancellationToken ct)
    {
        var dbCheck = await Measure("PostgreSQL", "Kalıcı veri (EF Core)", async () =>
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            return $"{await db.Users.CountAsync(ct)} kullanıcı, {await db.Tickets.CountAsync(ct)} talep";
        });
        var others = await Task.WhenAll(
            Measure("Redis", "Önbellek ve hız sınırlama", async () => $"PING {(await redis.GetDatabase().PingAsync()).TotalMilliseconds:0.0} ms"),
            Measure("RabbitMQ", "Olay kuyruğu", async () =>
            {
                var (messages, consumers) = await rabbit.InspectAsync(MessagingTopology.NotificationQueue, ct);
                return $"Kuyrukta {messages} mesaj · {consumers} tüketici";
            }),
            Measure("Worker", "Bildirim servisi", async () =>
            {
                var (_, consumers) = await rabbit.InspectAsync(MessagingTopology.NotificationQueue, ct);
                if (consumers == 0) throw new InvalidOperationException("Kuyruğu dinleyen Worker yok; mesajlar kuyrukta bekliyor.");
                return $"{consumers} aktif tüketici";
            }),
            Measure("Seq", "Merkezi loglama", async () =>
            {
                using var res = await http.CreateClient("external").GetAsync($"{(config["Seq:ServerUrl"] ?? "http://localhost:5341").TrimEnd('/')}/health", ct);
                res.EnsureSuccessStatusCode();
                return "Log toplama aktif";
            }));
        var pending = await db.Outbox.CountAsync(m => m.PublishedAt == null && m.Attempts < 20, ct);
        var failed = await db.Outbox.CountAsync(m => m.PublishedAt == null && m.Attempts >= 20, ct);
        var all = new[] { dbCheck }.Concat(others).ToList();
        return new SystemHealth(all.All(c => c.Healthy), DateTime.UtcNow, all, pending, failed);
    }

    public async Task<List<CacheEntry>> CacheAsync() =>
        (await cache.ListAsync(CacheKeys.Prefix)).Select(k => new CacheEntry(k.Key, k.Ttl?.TotalSeconds)).ToList();

    public Task<long> ClearCacheAsync(string? prefix)
    {
        var p = string.IsNullOrWhiteSpace(prefix) ? CacheKeys.Prefix : prefix;
        if (!p.StartsWith(CacheKeys.Prefix)) throw AppException.BadRequest("Yalnızca StajDesk önbellek anahtarları temizlenebilir.");
        if (p.StartsWith("stajdesk:ratelimit")) throw AppException.BadRequest("Güvenlik nedeniyle hız sınırlama sayaçları buradan temizlenemez.");
        return p == CacheKeys.Prefix ? ClearAllExceptRateLimitAsync() : cache.RemoveByPrefixAsync(p);
    }

    private async Task<long> ClearAllExceptRateLimitAsync()
    {
        long total = 0;
        foreach (var prefix in new[] { "stajdesk:lookup:", "stajdesk:stats:", "stajdesk:public:" }) total += await cache.RemoveByPrefixAsync(prefix);
        return total;
    }

    private static async Task<ComponentHealth> Measure(string name, string role, Func<Task<string>> probe)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var detail = await probe().WaitAsync(TimeSpan.FromSeconds(3));
            return new ComponentHealth(name, role, true, sw.ElapsedMilliseconds, detail);
        }
        catch (Exception ex)
        {
            return new ComponentHealth(name, role, false, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}

// ============================================================ Uygulama kataloğu
public class AppCatalogService(WorkflowService workflow)
{
    private record ModuleApp(string Key, string Name, string Module, string Icon, string Color, string Route, string? Description, string[]? AnyPermission = null);

    private static readonly ModuleApp[] Modules =
    [
        new("agent-management", "Agent Management", "IT", "Headset", "#2563eb", "/admin/groups", "Destek grupları ve ekip üyeleri.", [Permissions.OrgManage, Permissions.TicketsConfigure]),
        new("cache-clear", "Cache Clear", "IT", "Eraser", "#2563eb", "/system/cache", "Redis önbellek anahtarlarını görüntüleme ve temizleme.", [Permissions.SystemManage]),
        new("support-ticket", "Support Ticket", "IT", "LifeBuoy", "#2563eb", "/tickets/new", "BT destek talebi oluşturma."),
        new("limits", "Limitler", "Muhasebe", "Gauge", "#dc2626", "/apps/limits", "Avans ve masraf üst limitleri."),
        new("stores", "Mağaza Yönetimi", "Diğer Uygulamalar", "Store", "#64748b", "/stores", "Mağaza listesi, yönetici ve durum bilgileri."),
        new("on-call", "Nöbetçi Kişi", "Diğer Uygulamalar", "PhoneCall", "#64748b", "/on-call", "Mesai dışı nöbetçi personel çizelgesi."),
        new("faq", "SSS", "Diğer Uygulamalar", "CircleHelp", "#64748b", "/kb?faq=1", "Sık sorulan sorular."),
        new("tasks", "Görevler", "Diğer Uygulamalar", "ListChecks", "#64748b", "/tasks", "Kişisel ve proje görevleri (kanban)."),
        new("directory", "Çalışan Arama", "Diğer Uygulamalar", "Search", "#64748b", "/directory", "Çalışan rehberi."),
        new("notifications", "Bildirim Merkezi", "Diğer Uygulamalar", "Bell", "#64748b", "/notifications", "Tüm bildirimleriniz."),
        new("messages", "Mesajlaşma", "Diğer Uygulamalar", "MessagesSquare", "#64748b", "/messages", "Çalışanlar arası mesajlaşma."),
        new("documents", "Dokümanlar", "Diğer Uygulamalar", "FolderOpen", "#64748b", "/documents", "Politika, yönetmelik ve şablonlar."),
        new("reports", "Raporlama", "Diğer Uygulamalar", "ChartColumn", "#64748b", "/reports", "Talep, iş akışı ve envanter raporları.", [Permissions.ReportsView, Permissions.ReportsViewDepartment, Permissions.TicketsViewAll]),
        new("surveys", "Anketler", "Diğer Uygulamalar", "ClipboardList", "#64748b", "/surveys", "Çalışan anketleri."),
        new("jobs", "İş İlanları", "Diğer Uygulamalar", "Briefcase", "#64748b", "/jobs", "Açık pozisyonlar ve iç ilanlar."),
        new("events", "Etkinlikler", "Diğer Uygulamalar", "CalendarHeart", "#64748b", "/events", "Kurum içi etkinlikler."),
        new("achievements", "Çalışan Başarıları", "Diğer Uygulamalar", "Trophy", "#64748b", "/achievements", "Ödüller ve takdirler."),
        new("projects", "Projeler", "Strateji ve Dönüşüm Ofisi", "FolderKanban", "#0f766e", "/projects", "Proje portföyü ve ilerleme."),
    ];

    public async Task<List<AppEntry>> ListAsync(UserContext me, CancellationToken ct)
    {
        var forms = await workflow.FormsAsync(me, false, ct);
        var list = forms.Select(f => new AppEntry(f.Key, f.Name, f.Module, f.Icon, f.Color, $"/apps/{f.Key}", "form", f.Description, f.PendingCount)).ToList();
        list.AddRange(Modules.Where(m => m.AnyPermission is null || m.AnyPermission.Any(me.Has))
            .Select(m => new AppEntry(m.Key, m.Name, m.Module, m.Icon, m.Color, m.Route, "module", m.Description, 0)));
        string[] order = ["IT", "Genel Süreçler", "İnsan Kaynakları ve İdari İşler", "Kalite", "Lojistik", "Mağaza Muhasebesi", "Muhasebe", "Pazarlama", "Satın Alma", "Ürün", "Yönetim", "Strateji ve Dönüşüm Ofisi", "Yurtdışı Satış", "Yurtiçi Satış", "Diğer Uygulamalar"];
        return list.OrderBy(a => Array.IndexOf(order, a.Module) is var i && i < 0 ? 99 : i).ThenBy(a => a.Kind == "module").ThenBy(a => a.Name, StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), true)).ToList();
    }
}

// ============================================================ Controller'lar
[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard) : ApiController
{
    /// <summary>Rol ve yetkiye göre kişiselleştirilmiş özet panel.</summary>
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Ok(await dashboard.GetAsync(Me, ct));
}

[Route("api/reports")]
public class ReportsController(ReportService reports) : ApiController
{
    [HasPermission(Permissions.ReportsView, Permissions.ReportsViewDepartment, Permissions.TicketsViewAll), HttpGet("tickets")]
    public async Task<IActionResult> Tickets([FromQuery] ReportFilter f, CancellationToken ct) => Ok(await reports.TicketsAsync(f, Me, ct));

    [HasPermission(Permissions.ReportsView, Permissions.ReportsViewDepartment, Permissions.TicketsViewAll), HttpGet("tickets/export")]
    public async Task<IActionResult> Export([FromQuery] ReportFilter f, CancellationToken ct) =>
        File(await reports.TicketsCsvAsync(f, Me, ct), "text/csv; charset=utf-8", $"talepler-{DateTime.Now:yyyyMMdd-HHmm}.csv");

    [HasPermission(Permissions.ReportsView, Permissions.ReportsViewDepartment, Permissions.SubmissionsViewAll), HttpGet("workflows")]
    public async Task<IActionResult> Workflows([FromQuery] ReportFilter f, CancellationToken ct) => Ok(await reports.WorkflowsAsync(f, Me, ct));

    [HasPermission(Permissions.InventoryView, Permissions.ReportsView), HttpGet("inventory")]
    public async Task<IActionResult> Inventory(CancellationToken ct) => Ok(await reports.InventoryAsync(ct));

    [HasPermission(Permissions.ReportsView, Permissions.UsersManage, Permissions.LeaveManage), HttpGet("people")]
    public async Task<IActionResult> People(CancellationToken ct) => Ok(await reports.PeopleAsync(ct));
}

[Route("api/public")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public class PublicController(PublicSiteService site) : ApiController
{
    /// <summary>Genel tanıtım sitesi: aktif bannerlar, haberler, açık pozisyonlar ve şirket istatistikleri.</summary>
    [HttpGet("home")] public async Task<IActionResult> Home(CancellationToken ct) => Ok(await site.HomeAsync(ct));

    /// <summary>Gerçek kaynaklı piyasa verisi (TCMB, gold-api.com). Kaynağa ulaşılamazsa değer uydurulmaz, hata listelenir.</summary>
    [HttpGet("market")] public async Task<IActionResult> Market(CancellationToken ct) => Ok(await site.MarketAsync(ct));

    /// <summary>Open-Meteo hava durumu. Kaynağa ulaşılamazsa data=null döner.</summary>
    [HttpGet("weather")] public async Task<IActionResult> Weather([FromQuery] string city = "istanbul", CancellationToken ct = default) => Ok(await site.WeatherAsync(city, ct));
}

[Route("api/system")]
public class SystemController(SystemService system) : ApiController
{
    [HasPermission(Permissions.SystemManage), HttpGet("health")] public async Task<IActionResult> Health(CancellationToken ct) => Ok(await system.HealthAsync(ct));
    [HasPermission(Permissions.SystemManage), HttpGet("cache")] public async Task<IActionResult> Cache() => Ok(await system.CacheAsync());
    [HasPermission(Permissions.SystemManage), HttpDelete("cache")] public async Task<IActionResult> ClearCache([FromQuery] string? prefix) => Ok(new { removed = await system.ClearCacheAsync(prefix) }, "Önbellek temizlendi.");
}

[Route("api/apps")]
public class AppsController(AppCatalogService catalog) : ApiController
{
    /// <summary>Kullanıcının yetkisine göre departman uygulamaları ve modüller.</summary>
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await catalog.ListAsync(Me, ct));
}
