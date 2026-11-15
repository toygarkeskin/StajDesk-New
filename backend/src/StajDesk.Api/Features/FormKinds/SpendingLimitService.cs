using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Features;

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
