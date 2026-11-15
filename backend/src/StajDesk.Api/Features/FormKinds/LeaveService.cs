using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Features;

/// <summary>İzin bakiyesi, takvim ve izin formu kuralları (iş günü, resmî tatil, çakışma, bakiye; onayda bakiyeden düşme).</summary>
public class LeaveService(AppDbContext db, TimeProvider clock, ILogger<LeaveService> logger) : FormKindHandler
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public override IReadOnlyCollection<string> Kinds { get; } = [FormKinds.Leave];

    public async Task<LeaveBalanceDto> MyLeaveAsync(Guid userId, CancellationToken ct) =>
        (await LeaveBalancesAsync([userId], null, ct)).FirstOrDefault()
        ?? new LeaveBalanceDto(userId, "", null, Now.Year, 0, 0, 0, 0, 0);

    public async Task<List<LeaveBalanceDto>> LeaveBalancesAsync(List<Guid>? userIds, string? search, CancellationToken ct)
    {
        var year = Now.Year;
        var q = db.LeaveBalances.AsNoTracking().Where(b => b.Year == year && b.User.IsActive);
        if (userIds is not null) q = q.Where(b => userIds.Contains(b.UserId));
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(b => EF.Functions.ILike(b.User.FirstName + " " + b.User.LastName, search.Like()));
        var rows = await q.OrderBy(b => b.User.FirstName).Take(500).Select(b => new { b.UserId, Name = b.User.FirstName + " " + b.User.LastName, Dept = b.User.Department == null ? null : b.User.Department.Name, b.Year, b.Entitled, b.CarriedOver, b.Used }).ToListAsync(ct);
        var ids = rows.Select(r => r.UserId).ToList();
        var pending = await PendingAnnualDaysAsync(ids, ct);
        return rows.Select(r => new LeaveBalanceDto(r.UserId, r.Name, r.Dept, r.Year, r.Entitled, r.CarriedOver, r.Used, pending.GetValueOrDefault(r.UserId),
            r.Entitled + r.CarriedOver - r.Used)).ToList();
    }

    public async Task<LeaveBalanceDto> UpdateLeaveAsync(Guid userId, UpdateLeaveBalanceRequest req, CancellationToken ct)
    {
        if (req.Entitled is < 0 or > 60 || req.CarriedOver is < 0 or > 60) throw AppException.BadRequest("Gün sayıları 0-60 arasında olmalıdır.");
        var b = await db.LeaveBalances.FirstOrDefaultAsync(x => x.UserId == userId && x.Year == Now.Year, ct);
        if (b is null) db.LeaveBalances.Add(b = new LeaveBalance { UserId = userId, Year = Now.Year });
        b.Entitled = req.Entitled;
        b.CarriedOver = req.CarriedOver;
        await db.SaveChangesAsync(ct);
        return await MyLeaveAsync(userId, ct);
    }

    public async Task<List<LeaveCalendarEntry>> LeaveCalendarAsync(UserContext me, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var subs = await db.FormSubmissions.AsNoTracking()
            .Where(s => s.FormDefinition.Kind == FormKinds.Leave && WorkflowData.LiveStatuses.Contains(s.Status)
                        && (s.DepartmentId == me.DepartmentId || s.SubmitterId == me.Id))
            .Select(s => new { s.Id, s.Data, s.Status, User = new UserRef(s.Submitter.Id, s.Submitter.FirstName + " " + s.Submitter.LastName, s.Submitter.Email) })
            .ToListAsync(ct);
        var list = new List<LeaveCalendarEntry>();
        foreach (var s in subs)
        {
            var d = WorkflowData.Parse(s.Data);
            if (WorkflowData.DateOf(d, "startDate") is not { } start || WorkflowData.DateOf(d, "endDate") is not { } end) continue;
            if (end < from || start > to) continue;
            list.Add(new LeaveCalendarEntry(s.Id, s.User, d.GetValueOrDefault("leaveType").ToString(), start, end, LeaveRules.WorkingDays(start, end), s.Status));
        }
        return list.OrderBy(x => x.Start).ToList();
    }

    /// <summary>Kullanıcının verilen günde (bekleyen veya onaylı) izni var mı?</summary>
    public async Task<bool> HasLeaveOnAsync(Guid userId, DateOnly date, CancellationToken ct)
    {
        var leaves = await db.FormSubmissions.AsNoTracking()
            .Where(x => x.SubmitterId == userId && x.FormDefinition.Kind == FormKinds.Leave && WorkflowData.LiveStatuses.Contains(x.Status))
            .Select(x => x.Data).ToListAsync(ct);
        return leaves.Select(WorkflowData.Parse)
            .Any(d => WorkflowData.DateOf(d, "startDate") is { } s && WorkflowData.DateOf(d, "endDate") is { } e && date >= s && date <= e);
    }

    public override async Task<string?> ValidateAsync(FormSubmitContext ctx, CancellationToken ct)
    {
        var (data, me) = (ctx.Data, ctx.Me);
        var start = DateOnly.Parse((string)data["startDate"]!, CultureInfo.InvariantCulture);
        var end = DateOnly.Parse((string)data["endDate"]!, CultureInfo.InvariantCulture);
        var type = LeaveRules.Parse(data["leaveType"] as string) ?? throw AppException.BadRequest("Geçersiz izin türü.");
        if (end < start) throw new ValidationAppException(new() { ["endDate"] = ["Bitiş tarihi başlangıçtan önce olamaz."] });
        if (start < DateOnly.FromDateTime(Now).AddDays(-30)) throw new ValidationAppException(new() { ["startDate"] = ["30 günden eski tarihli izin girilemez."] });
        var days = LeaveRules.WorkingDays(start, end);
        if (days == 0) throw new ValidationAppException(new() { ["endDate"] = ["Seçilen aralıkta iş günü bulunmuyor (hafta sonu / resmî tatil)."] });

        // Çakışan izin kontrolü
        var existing = await db.FormSubmissions.AsNoTracking()
            .Where(s => s.SubmitterId == me.Id && s.FormDefinition.Kind == FormKinds.Leave && WorkflowData.LiveStatuses.Contains(s.Status))
            .Select(s => s.Data).ToListAsync(ct);
        foreach (var d in existing.Select(WorkflowData.Parse))
        {
            if (WorkflowData.DateOf(d, "startDate") is { } s2 && WorkflowData.DateOf(d, "endDate") is { } e2 && start <= e2 && end >= s2)
                throw AppException.BadRequest($"Bu tarihlerde ({s2:dd.MM.yyyy} - {e2:dd.MM.yyyy}) başka bir izin talebiniz bulunuyor.");
        }
        if (type == LeaveType.Annual)
        {
            var bal = await MyLeaveAsync(me.Id, ct);
            var available = bal.Remaining - bal.Pending;
            if (days > available) throw AppException.BadRequest($"Yıllık izin bakiyeniz yetersiz. Talep edilen: {days} gün, kullanılabilir: {available:0.#} gün.");
        }
        if (type == LeaveType.Excuse && days > 3) throw AppException.BadRequest("Mazeret izni en fazla 3 iş günü olabilir.");
        data["days"] = days;
        return $"{LeaveRules.Label(type)} · {start:dd.MM.yyyy} - {end:dd.MM.yyyy} · {days} iş günü";
    }

    /// <summary>Yıllık izin onaylanınca bakiyeden düşülür.</summary>
    public override async Task OnApprovedAsync(FormSubmission s, Dictionary<string, JsonElement> data, CancellationToken ct)
    {
        if (LeaveRules.Parse(data.GetValueOrDefault("leaveType").ToString()) != LeaveType.Annual || !data.TryGetValue("days", out var d)) return;
        var year = (WorkflowData.DateOf(data, "startDate") ?? DateOnly.FromDateTime(Now)).Year;
        var balance = await db.LeaveBalances.FirstOrDefaultAsync(b => b.UserId == s.SubmitterId && b.Year == year, ct);
        if (balance is null) db.LeaveBalances.Add(balance = new LeaveBalance { UserId = s.SubmitterId, Year = year });
        balance.Used += d.GetDecimal();
        logger.LogInformation("İzin bakiyesinden düşüldü {UserId} {Days} gün", s.SubmitterId, d.GetDecimal());
    }

    public override async Task<FormContext?> UserContextAsync(FormDefinition form, Guid userId, CancellationToken ct)
    {
        var b = await MyLeaveAsync(userId, ct);
        return new FormContext(b.Remaining - b.Pending, b.Entitled + b.CarriedOver, b.Used, b.Pending, null, null);
    }

    public override IEnumerable<SubmissionField> ExtraFields(Dictionary<string, JsonElement> data)
    {
        if (data.TryGetValue("days", out var days) && days.ValueKind == JsonValueKind.Number)
            yield return new SubmissionField("days", "Hesaplanan iş günü", FieldTypes.Number, days, $"{days.GetDecimal().ToString("0.#", WorkflowData.Tr)} gün");
    }

    private async Task<Dictionary<Guid, decimal>> PendingAnnualDaysAsync(List<Guid> userIds, CancellationToken ct)
    {
        var pending = await db.FormSubmissions.AsNoTracking()
            .Where(s => s.FormDefinition.Kind == FormKinds.Leave && s.Status == SubmissionStatus.Pending && userIds.Contains(s.SubmitterId))
            .Select(s => new { s.SubmitterId, s.Data }).ToListAsync(ct);
        return pending.Select(p => (p.SubmitterId, Data: WorkflowData.Parse(p.Data)))
            .Where(p => LeaveRules.Parse(p.Data.GetValueOrDefault("leaveType").ToString()) == LeaveType.Annual && p.Data.ContainsKey("days"))
            .GroupBy(p => p.SubmitterId).ToDictionary(g => g.Key, g => g.Sum(p => p.Data["days"].GetDecimal()));
    }
}
