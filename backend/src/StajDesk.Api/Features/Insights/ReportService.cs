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
