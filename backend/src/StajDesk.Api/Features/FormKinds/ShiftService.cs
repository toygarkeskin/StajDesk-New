using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Features;

/// <summary>Vardiya değişikliği kuralları ve ekip vardiya listesi.</summary>
public class ShiftService(AppDbContext db, LeaveService leave, TimeProvider clock) : FormKindHandler
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public override IReadOnlyCollection<string> Kinds { get; } = [FormKinds.Shift];

    /// <summary>
    /// Tarih bugünden 60 gün sonrasına kadar olmalı, istenen vardiya farklı olmalı, aynı gün için ikinci talep ve izinli olunan güne talep yapılamaz.
    /// Takas yapılacak kişi aynı departmandan, aktif ve o gün izinsiz olmalıdır; akışın ilk adımında ("formda seçilen kişi") onaylar.
    /// </summary>
    public override async Task<string?> ValidateAsync(FormSubmitContext ctx, CancellationToken ct)
    {
        var (data, me) = (ctx.Data, ctx.Me);
        var date = DateOnly.Parse((string)data["date"]!, CultureInfo.InvariantCulture);
        var today = BusinessTime.DateOf(Now);
        if (date < today) throw new ValidationAppException(new() { ["date"] = ["Geçmiş tarih için vardiya değişikliği talep edilemez."] });
        if (date > today.AddDays(60)) throw new ValidationAppException(new() { ["date"] = ["En fazla 60 gün sonrası için talep oluşturabilirsiniz."] });
        var from = data.GetValueOrDefault("currentShift") as string;
        var to = data.GetValueOrDefault("requestedShift") as string;
        if (from == to) throw new ValidationAppException(new() { ["requestedShift"] = ["İstenen vardiya mevcut vardiyanızdan farklı olmalıdır."] });

        if (await leave.HasLeaveOnAsync(me.Id, date, ct)) throw AppException.BadRequest($"{date:dd.MM.yyyy} tarihinde izin kaydınız bulunuyor; vardiya değişikliği gerekmez.");

        var mine = await db.FormSubmissions.AsNoTracking()
            .Where(x => x.SubmitterId == me.Id && x.FormDefinition.Kind == FormKinds.Shift && WorkflowData.LiveStatuses.Contains(x.Status))
            .Select(x => new { x.Number, x.Data }).ToListAsync(ct);
        foreach (var e in mine)
            if (WorkflowData.DateOf(WorkflowData.Parse(e.Data), "date") == date)
                throw AppException.Conflict($"{date:dd.MM.yyyy} için zaten bir vardiya talebiniz var ({e.Number}).");

        var summary = $"{date:dd.MM.yyyy} · {from} → {to}";
        if (data.GetValueOrDefault("swapWith") is FieldUserValue swap)
        {
            if (swap.Id == me.Id) throw new ValidationAppException(new() { ["swapWith"] = ["Kendinizle takas yapamazsınız."] });
            var swapDept = await db.Users.Where(u => u.Id == swap.Id).Select(u => u.DepartmentId).FirstAsync(ct);
            if (swapDept != me.DepartmentId) throw new ValidationAppException(new() { ["swapWith"] = ["Takas yalnızca aynı departmandaki çalışanlarla yapılabilir."] });
            if (await leave.HasLeaveOnAsync(swap.Id, date, ct)) throw new ValidationAppException(new() { ["swapWith"] = [$"{swap.Name} bu tarihte izinli."] });
            summary += $" · takas: {swap.Name}";
        }
        return summary;
    }

    /// <summary>Kendi departmanının vardiya değişiklikleri (takaslar dahil); İK / izin yöneticileri tüm şirketi görür.</summary>
    public async Task<List<ShiftChangeEntry>> ChangesAsync(UserContext me, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var all = me.IsAdmin || me.Has(Permissions.LeaveManage);
        var q = db.FormSubmissions.AsNoTracking().Where(x => x.FormDefinition.Kind == FormKinds.Shift && WorkflowData.LiveStatuses.Contains(x.Status));
        // Takas yalnızca aynı departman içinde yapılabildiği için departman filtresi yeterlidir
        if (!all) q = q.Where(x => x.DepartmentId == me.DepartmentId || x.SubmitterId == me.Id);
        var subs = await q.Select(x => new { x.Id, x.Data, x.Status, User = new UserRef(x.Submitter.Id, x.Submitter.FirstName + " " + x.Submitter.LastName, x.Submitter.Email) })
            .ToListAsync(ct);
        var list = new List<ShiftChangeEntry>();
        foreach (var x in subs)
        {
            var d = WorkflowData.Parse(x.Data);
            if (WorkflowData.DateOf(d, "date") is not { } date || date < from || date > to) continue;
            var swap = d.TryGetValue("swapWith", out var sw) && sw.ValueKind == JsonValueKind.Object && sw.TryGetProperty("name", out var n) ? n.GetString() : null;
            list.Add(new ShiftChangeEntry(x.Id, x.User, date, d.GetValueOrDefault("currentShift").ToString(), d.GetValueOrDefault("requestedShift").ToString(), swap, x.Status));
        }
        return list.OrderBy(x => x.Date).ToList();
    }
}
