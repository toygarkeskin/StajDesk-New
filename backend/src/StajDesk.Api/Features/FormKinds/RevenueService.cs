using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Features;

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
