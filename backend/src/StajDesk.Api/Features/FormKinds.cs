using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Features;

// =====================================================================================================
// Özel form türleri (strateji deseni)
//
// İş akışı motoru (WorkflowService) türden bağımsızdır: alan doğrulama, onay adımları, bildirimler.
// Türe özgü kurallar IFormKindHandler uygulamalarındadır. Yeni bir tür eklemek için motoru değiştirmeye
// gerek yoktur (Açık/Kapalı ilkesi): yeni bir handler yazıp DI'a kaydetmek yeterlidir.
// =====================================================================================================

/// <summary>Başvuru gönderilirken türe özgü doğrulamaya verilen bağlam. <see cref="Data"/> normalleştirilmiş form verisidir ve değiştirilebilir.</summary>
public sealed record FormSubmitContext(FormDefinition Form, Dictionary<string, object?> Data, decimal? Amount, UserContext Me);

public interface IFormKindHandler
{
    /// <summary>Bu işleyicinin sorumlu olduğu form türleri (<see cref="FormKinds"/>).</summary>
    IReadOnlyCollection<string> Kinds { get; }

    /// <summary>Türe özgü kuralları uygular; geçersizse istisna fırlatır. Özel bir özet üretirse döner, yoksa null.</summary>
    Task<string?> ValidateAsync(FormSubmitContext ctx, CancellationToken ct);

    /// <summary>Tüm onay adımları tamamlanınca çalışır (ör. izin bakiyesinden düşme).</summary>
    Task OnApprovedAsync(FormSubmission submission, Dictionary<string, JsonElement> data, CancellationToken ct);

    /// <summary>Başvuru ekranında kullanıcıya gösterilecek bağlam (bakiye, limit vb.).</summary>
    Task<FormContext?> UserContextAsync(FormDefinition form, Guid userId, CancellationToken ct);

    /// <summary>Form alanlarını istemciye göndermeden önce zenginleştirir (ör. seçenekleri veritabanından doldurma).</summary>
    Task<IReadOnlyList<FormField>> PrepareFieldsAsync(FormDefinition form, CancellationToken ct);

    /// <summary>Başvuru detayında gösterilecek hesaplanmış ek alanlar.</summary>
    IEnumerable<SubmissionField> ExtraFields(Dictionary<string, JsonElement> data);
}

/// <summary>Varsayılan (etkisiz) davranışlar; işleyiciler yalnızca ihtiyaç duydukları adımları ezer.</summary>
public abstract class FormKindHandler : IFormKindHandler
{
    public abstract IReadOnlyCollection<string> Kinds { get; }
    public virtual Task<string?> ValidateAsync(FormSubmitContext ctx, CancellationToken ct) => Task.FromResult<string?>(null);
    public virtual Task OnApprovedAsync(FormSubmission submission, Dictionary<string, JsonElement> data, CancellationToken ct) => Task.CompletedTask;
    public virtual Task<FormContext?> UserContextAsync(FormDefinition form, Guid userId, CancellationToken ct) => Task.FromResult<FormContext?>(null);
    public virtual Task<IReadOnlyList<FormField>> PrepareFieldsAsync(FormDefinition form, CancellationToken ct) => Task.FromResult<IReadOnlyList<FormField>>(form.Fields);
    public virtual IEnumerable<SubmissionField> ExtraFields(Dictionary<string, JsonElement> data) => [];
}

/// <summary>Standart formlar: ek kural yok.</summary>
public sealed class StandardFormHandler : FormKindHandler
{
    public override IReadOnlyCollection<string> Kinds { get; } = [FormKinds.Standard];
}

/// <summary>Türe göre işleyici seçer. Kaydı olmayan tür standart davranışa düşer.</summary>
public sealed class FormKindRegistry
{
    private readonly Dictionary<string, IFormKindHandler> _handlers;
    private static readonly StandardFormHandler Standard = new();

    public FormKindRegistry(IEnumerable<IFormKindHandler> handlers)
    {
        _handlers = new(StringComparer.OrdinalIgnoreCase);
        foreach (var h in handlers)
            foreach (var k in h.Kinds)
                if (!_handlers.TryAdd(k, h)) throw new InvalidOperationException($"'{k}' form türü için birden fazla işleyici kayıtlı.");
    }

    public IFormKindHandler For(string kind) => _handlers.GetValueOrDefault(kind) ?? Standard;
}

/// <summary>İş akışı verisi için ortak yardımcılar.</summary>
public static class WorkflowData
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    /// <summary>"Yaşayan" başvurular: reddedilen ve iptal edilenler hariç.</summary>
    public static readonly SubmissionStatus[] LiveStatuses = [SubmissionStatus.Pending, SubmissionStatus.Approved, SubmissionStatus.Completed];

    public static Dictionary<string, JsonElement> Parse(string json) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, Json) ?? [];
    public static decimal? Dec(Dictionary<string, object?> data, string key) => data.GetValueOrDefault(key) is decimal d ? d : null;
    public static DateOnly? DateOf(Dictionary<string, JsonElement> d, string key) =>
        d.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String && DateOnly.TryParse(v.GetString(), CultureInfo.InvariantCulture, out var x) ? x : null;
}

// ============================================================ İzin
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

// ============================================================ Avans / masraf limitleri
/// <summary>Harcama limitleri ve avans/masraf formlarının limit kontrolü.</summary>
public class SpendingLimitService(AppDbContext db) : FormKindHandler
{
    public override IReadOnlyCollection<string> Kinds { get; } = [FormKinds.Expense, FormKinds.Advance];

    public Task<List<LimitDto>> LimitsAsync(CancellationToken ct) =>
        db.SpendingLimits.AsNoTracking().OrderBy(l => l.Kind).Select(l => new LimitDto(l.Id, l.Kind, l.Label, l.MaxAmount, l.Currency)).ToListAsync(ct);

    public async Task<LimitDto> UpdateLimitAsync(int id, UpdateLimitRequest req, CancellationToken ct)
    {
        if (req.MaxAmount is <= 0 or > 10_000_000) throw AppException.BadRequest("Limit 0'dan büyük ve 10.000.000'dan küçük olmalıdır.");
        var l = await db.SpendingLimits.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Limit");
        l.MaxAmount = req.MaxAmount;
        await db.SaveChangesAsync(ct);
        return new LimitDto(l.Id, l.Kind, l.Label, l.MaxAmount, l.Currency);
    }

    public override async Task<string?> ValidateAsync(FormSubmitContext ctx, CancellationToken ct)
    {
        var kind = ctx.Form.Kind;
        var limit = await db.SpendingLimits.AsNoTracking().FirstOrDefaultAsync(l => l.Kind == kind, ct);
        if (limit is not null && ctx.Amount > limit.MaxAmount)
            throw new ValidationAppException(new() { ["amount"] = [$"Tutar {limit.Label.ToLowerInvariant()} olan {limit.MaxAmount.ToString("N0", WorkflowData.Tr)} ₺'yi aşıyor."] });
        if (kind == FormKinds.Advance && await db.FormSubmissions.AnyAsync(s => s.SubmitterId == ctx.Me.Id && s.FormDefinition.Kind == FormKinds.Advance && s.Status == SubmissionStatus.Pending, ct))
            throw AppException.BadRequest("Onay bekleyen bir avans talebiniz varken yeni talep oluşturamazsınız.");
        return null;
    }

    public override async Task<FormContext?> UserContextAsync(FormDefinition form, Guid userId, CancellationToken ct)
    {
        var l = await db.SpendingLimits.AsNoTracking().FirstOrDefaultAsync(x => x.Kind == form.Kind, ct);
        return new FormContext(null, null, null, null, l?.MaxAmount, l?.Label);
    }
}

// ============================================================ Ciro
/// <summary>Mağaza ciro bildirimi kuralları ve ciro özeti.</summary>
public class RevenueService(AppDbContext db, TimeProvider clock) : FormKindHandler
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public override IReadOnlyCollection<string> Kinds { get; } = [FormKinds.Revenue];

    /// <summary>Mağaza alanı, açık mağazalar listesinden seçilir (sunucu ayrıca doğrular).</summary>
    public override async Task<IReadOnlyList<FormField>> PrepareFieldsAsync(FormDefinition form, CancellationToken ct)
    {
        var stores = await db.Stores.AsNoTracking().Where(x => x.Status != StoreStatus.Closed).OrderBy(x => x.Name).Select(x => x.Name).ToListAsync(ct);
        return form.Fields.Select(x => x.Key == "storeName"
            ? new FormField { Key = x.Key, Label = x.Label, Type = FieldTypes.Select, Required = x.Required, Options = stores, HelpText = x.HelpText ?? "Yalnızca açık mağazalar listelenir." }
            : x).ToList();
    }

    /// <summary>
    /// Mağaza açık ve kayıtlı olmalı, tarih ileri / 45 günden eski olamaz, nakit + kart girildiyse toplamla eşleşmeli,
    /// aynı mağaza ve gün için ikinci bildirim yapılamaz. Ortalama sepet tutarı hesaplanıp kaydedilir.
    /// </summary>
    public override async Task<string?> ValidateAsync(FormSubmitContext ctx, CancellationToken ct)
    {
        var data = ctx.Data;
        var tr = WorkflowData.Tr;
        var storeName = (data.GetValueOrDefault("storeName") as string)?.Trim() ?? "";
        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(x => x.Name.ToLower() == storeName.ToLower() && x.Status != StoreStatus.Closed, ct)
                    ?? throw new ValidationAppException(new() { ["storeName"] = ["Mağaza bulunamadı veya kapalı. Lütfen listeden seçiniz."] });
        var date = DateOnly.Parse((string)data["date"]!, CultureInfo.InvariantCulture);
        var today = DateOnly.FromDateTime(Now);
        if (date > today) throw new ValidationAppException(new() { ["date"] = ["İleri tarihli ciro bildirimi yapılamaz."] });
        if (date < today.AddDays(-45)) throw new ValidationAppException(new() { ["date"] = ["45 günden eski ciro için Mağaza Muhasebesi ile iletişime geçiniz."] });

        var amount = WorkflowData.Dec(data, "amount") ?? 0;
        if (amount <= 0) throw new ValidationAppException(new() { ["amount"] = ["Toplam ciro sıfırdan büyük olmalıdır."] });
        var cash = WorkflowData.Dec(data, "cashAmount");
        var card = WorkflowData.Dec(data, "cardAmount");
        if (cash is not null || card is not null)
        {
            var sum = (cash ?? 0) + (card ?? 0);
            if (Math.Abs(sum - amount) > 0.01m)
                throw new ValidationAppException(new() { ["amount"] = [$"Toplam ciro, nakit ve kart toplamına ({sum.ToString("C2", tr)}) eşit olmalıdır."] });
        }

        // Mükerrer bildirim: aynı mağaza + gün (reddedilen / iptal edilenler hariç)
        var since = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var existing = await db.FormSubmissions.AsNoTracking()
            .Where(x => x.FormDefinition.Kind == FormKinds.Revenue && WorkflowData.LiveStatuses.Contains(x.Status) && x.CreatedAt >= since)
            .Select(x => new { x.Number, x.Data }).ToListAsync(ct);
        foreach (var e in existing)
        {
            var d = WorkflowData.Parse(e.Data);
            if (WorkflowData.DateOf(d, "date") == date && string.Equals(d.GetValueOrDefault("storeName").ToString(), store.Name, StringComparison.OrdinalIgnoreCase))
                throw AppException.Conflict($"{store.Name} için {date:dd.MM.yyyy} tarihli ciro bildirimi zaten var ({e.Number}).");
        }

        data["storeName"] = store.Name;
        data["storeCode"] = store.Code;
        var summary = $"{store.Name} · {date:dd.MM.yyyy} · {amount.ToString("C2", tr)}";
        if (WorkflowData.Dec(data, "receiptCount") is { } receipts && receipts > 0)
        {
            var avg = Math.Round(amount / receipts, 2);
            data["averageBasket"] = avg;
            summary += $" · {receipts:0} fiş · ort. sepet {avg.ToString("C2", tr)}";
        }
        return summary;
    }

    /// <summary>
    /// Ciro özeti. Şirket geneli: rapor yetkisi, form yöneticisi veya sorumlu birim (Mağaza Muhasebesi) üyeleri; diğerleri yalnızca kendi bildirimlerini görür.
    /// Toplamlara onaylanmış/tamamlanmış bildirimler girer; bekleyenler ayrıca sayılır.
    /// </summary>
    public async Task<RevenueSummary> SummaryAsync(UserContext me, int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 7, 180);
        var form = await db.FormDefinitions.AsNoTracking().FirstOrDefaultAsync(f => f.Kind == FormKinds.Revenue, ct) ?? throw AppException.NotFound("Ciro formu");
        var companyWide = me.IsAdmin || me.Has(Permissions.ReportsView)
                          || (me.Has(Permissions.FormsManage) && form.OwnerDepartmentId == me.DepartmentId)
                          || (form.OwnerDepartmentId is { } od && me.DepartmentId == od);
        var to = DateOnly.FromDateTime(Now);
        var from = to.AddDays(-(days - 1));
        var since = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var q = db.FormSubmissions.AsNoTracking().Where(x => x.FormDefinitionId == form.Id && WorkflowData.LiveStatuses.Contains(x.Status) && x.CreatedAt >= since);
        if (!companyWide) q = q.Where(x => x.SubmitterId == me.Id);
        var rows = (await q.Select(x => new { x.Status, x.Data }).ToListAsync(ct))
            .Select(x => (x.Status, Data: WorkflowData.Parse(x.Data)))
            .Select(x => (x.Status, Date: WorkflowData.DateOf(x.Data, "date"), Store: x.Data.GetValueOrDefault("storeName").ToString(),
                Amount: x.Data.TryGetValue("amount", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDecimal() : 0,
                Receipts: x.Data.TryGetValue("receiptCount", out var r) && r.ValueKind == JsonValueKind.Number ? (int)r.GetDecimal() : 0))
            .Where(x => x.Date >= from && x.Date <= to).ToList();
        var done = rows.Where(x => x.Status != SubmissionStatus.Pending).ToList();
        var receipts = done.Sum(x => x.Receipts);
        var receiptAmount = done.Where(x => x.Receipts > 0).Sum(x => x.Amount);
        var byDay = Enumerable.Range(0, days).Select(i => from.AddDays(i))
            .Select(d => new RevenueDay(d, done.Where(x => x.Date == d).Sum(x => x.Amount))).ToList();
        var byStore = done.GroupBy(x => x.Store).Select(g =>
        {
            var rc = g.Sum(x => x.Receipts);
            var ra = g.Where(x => x.Receipts > 0).Sum(x => x.Amount);
            return new RevenueStore(g.Key, g.Sum(x => x.Amount), g.Count(), rc, rc > 0 ? Math.Round(ra / rc, 2) : null);
        }).OrderByDescending(x => x.Amount).Take(10).ToList();
        return new RevenueSummary(from, to, companyWide, done.Sum(x => x.Amount), done.Count, receipts, receipts > 0 ? Math.Round(receiptAmount / receipts, 2) : null,
            rows.Count(x => x.Status == SubmissionStatus.Pending), byDay, byStore);
    }
}

// ============================================================ Vardiya
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
        var today = DateOnly.FromDateTime(Now);
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
