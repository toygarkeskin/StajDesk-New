using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public record AssetDto(int Id, string Name, string AssetTag, AssetType Type, AssetStatus Status, string? Manufacturer, string? Model, string? SerialNumber,
    DeptRef? Department, string? Location, UserRef? AssignedTo, DateOnly? PurchaseDate, DateOnly? WarrantyEndsAt, decimal? PurchaseCost, string? Notes,
    DateTime CreatedAt, DateTime UpdatedAt, int OpenTicketCount);
public record AssignmentDto(long Id, UserRef User, UserRef AssignedBy, DateTime AssignedAt, DateTime? ReturnedAt, UserRef? ReturnedBy, string? Note, string? ReturnCondition);
public record InstalledSoftwareDto(long Id, int LicenseId, string Name, string? Version, DateTime InstalledAt);
public record AssetDetailDto(AssetDto Asset, IReadOnlyList<AssignmentDto> Assignments, IReadOnlyList<InstalledSoftwareDto> Software, IReadOnlyList<TicketListItem> Tickets);
public record SaveAssetRequest(string Name, string AssetTag, AssetType Type, AssetStatus Status, string? Manufacturer, string? Model, string? SerialNumber,
    int? DepartmentId, string? Location, DateOnly? PurchaseDate, DateOnly? WarrantyEndsAt, decimal? PurchaseCost, string? Notes);
public record CheckoutRequest(Guid UserId, string? Note);
public record CheckinRequest(string? Condition, string? Note, AssetStatus NewStatus = AssetStatus.InStock);
public record LicenseDto(int Id, string Name, string? Vendor, string? Version, int Seats, int Used, DateOnly? ExpiresAt, decimal? Cost, string? Notes, bool IsExpired);
public record SaveLicenseRequest(string Name, string? Vendor, string? Version, int Seats, DateOnly? ExpiresAt, decimal? Cost, string? Notes);
public record InstallRequest(int AssetId);

public class AssetQuery : PagedQuery
{
    public AssetType? Type { get; set; }
    public AssetStatus? Status { get; set; }
    public int? DepartmentId { get; set; }
    public Guid? AssignedToId { get; set; }
    public bool? WarrantyExpiring { get; set; }
    public bool Mine { get; set; }
}

public class SaveAssetValidator : AbstractValidator<SaveAssetRequest>
{
    public SaveAssetValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Varlık adı zorunludur.").MaximumLength(150);
        RuleFor(x => x.AssetTag).NotEmpty().WithMessage("Envanter numarası zorunludur.").Matches("^[A-Za-z0-9-]{3,40}$").WithMessage("Envanter numarası harf, rakam ve tire içerebilir.");
        RuleFor(x => x.PurchaseCost).GreaterThanOrEqualTo(0).When(x => x.PurchaseCost.HasValue).WithMessage("Tutar negatif olamaz.");
        RuleFor(x => x.WarrantyEndsAt).GreaterThanOrEqualTo(x => x.PurchaseDate).When(x => x.PurchaseDate.HasValue && x.WarrantyEndsAt.HasValue).WithMessage("Garanti bitişi satın alma tarihinden önce olamaz.");
    }
}

public class SaveLicenseValidator : AbstractValidator<SaveLicenseRequest>
{
    public SaveLicenseValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Lisans adı zorunludur.").MaximumLength(150);
        RuleFor(x => x.Seats).InclusiveBetween(1, 100000).WithMessage("Lisans adedi 1 ile 100.000 arasında olmalıdır.");
    }
}

public class InventoryService(AppDbContext db, IEventBus events, TimeProvider clock, ILogger<InventoryService> logger)
{
    private static readonly System.Linq.Expressions.Expression<Func<Asset, AssetDto>> Map = a => new AssetDto(a.Id, a.Name, a.AssetTag, a.Type, a.Status, a.Manufacturer, a.Model,
        a.SerialNumber, a.Department == null ? null : new DeptRef(a.Department.Id, a.Department.Name), a.Location,
        a.AssignedTo == null ? null : new UserRef(a.AssignedTo.Id, a.AssignedTo.FirstName + " " + a.AssignedTo.LastName, a.AssignedTo.Email),
        a.PurchaseDate, a.WarrantyEndsAt, a.PurchaseCost, a.Notes, a.CreatedAt, a.UpdatedAt,
        a.Tickets.Count(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed));

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public Task<PagedResult<AssetDto>> ListAsync(AssetQuery q, UserContext me, CancellationToken ct)
    {
        var query = db.Assets.AsNoTracking().AsQueryable();
        // Envanter yetkisi olmayan çalışan yalnızca kendi zimmetini görür.
        if (!me.Has(Permissions.InventoryView) || q.Mine) query = query.Where(a => a.AssignedToId == me.Id);
        if (q.Type is { } t) query = query.Where(a => a.Type == t);
        if (q.Status is { } s) query = query.Where(a => a.Status == s);
        if (q.DepartmentId is { } d) query = query.Where(a => a.DepartmentId == d);
        if (q.AssignedToId is { } u) query = query.Where(a => a.AssignedToId == u);
        if (q.WarrantyExpiring == true)
        {
            var today = DateOnly.FromDateTime(Now);
            query = query.Where(a => a.WarrantyEndsAt != null && a.WarrantyEndsAt >= today && a.WarrantyEndsAt <= today.AddDays(90));
        }
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Like();
            query = query.Where(a => EF.Functions.ILike(a.Name, term) || EF.Functions.ILike(a.AssetTag, term) || (a.SerialNumber != null && EF.Functions.ILike(a.SerialNumber, term))
                                     || (a.AssignedTo != null && EF.Functions.ILike(a.AssignedTo.FirstName + " " + a.AssignedTo.LastName, term)));
        }
        return query.OrderBy(a => a.AssetTag).ToPagedAsync(q, Map, ct);
    }

    public async Task<AssetDetailDto> GetAsync(int id, UserContext me, CancellationToken ct)
    {
        var asset = await db.Assets.AsNoTracking().Where(a => a.Id == id).Select(Map).FirstOrDefaultAsync(ct) ?? throw AppException.NotFound("Varlık");
        if (!me.Has(Permissions.InventoryView) && asset.AssignedTo?.Id != me.Id) throw AppException.Forbidden();
        var history = await db.AssetAssignments.AsNoTracking().Where(x => x.AssetId == id).OrderByDescending(x => x.AssignedAt)
            .Select(x => new AssignmentDto(x.Id, new UserRef(x.User.Id, x.User.FirstName + " " + x.User.LastName, x.User.Email),
                new UserRef(x.AssignedBy.Id, x.AssignedBy.FirstName + " " + x.AssignedBy.LastName, x.AssignedBy.Email), x.AssignedAt, x.ReturnedAt,
                x.ReturnedBy == null ? null : new UserRef(x.ReturnedBy.Id, x.ReturnedBy.FirstName + " " + x.ReturnedBy.LastName, x.ReturnedBy.Email), x.Note, x.ReturnCondition))
            .ToListAsync(ct);
        var software = await db.SoftwareInstallations.AsNoTracking().Where(s => s.AssetId == id)
            .Select(s => new InstalledSoftwareDto(s.Id, s.LicenseId, s.License.Name, s.License.Version, s.InstalledAt)).ToListAsync(ct);
        var tickets = await db.Tickets.AsNoTracking().Where(t => t.AssetId == id && (me.Has(Permissions.TicketsViewAll) || t.RequesterId == me.Id))
            .OrderByDescending(t => t.CreatedAt).Take(20).Select(TicketService.Projection(Now)).ToListAsync(ct);
        return new AssetDetailDto(asset, history, software, tickets);
    }

    public async Task<AssetDto> SaveAsync(int? id, SaveAssetRequest req, CancellationToken ct)
    {
        var tag = req.AssetTag.Trim().ToUpperInvariant();
        if (await db.Assets.AnyAsync(a => a.AssetTag == tag && a.Id != id, ct)) throw AppException.Conflict($"'{tag}' envanter numarası başka bir varlıkta kullanılıyor.");
        Asset a;
        if (id is { } e) a = await db.Assets.FirstOrDefaultAsync(x => x.Id == e, ct) ?? throw AppException.NotFound("Varlık");
        else db.Assets.Add(a = new Asset { Name = req.Name, AssetTag = tag });
        if (a.AssignedToId is not null && req.Status is AssetStatus.InStock or AssetStatus.Retired)
            throw AppException.BadRequest("Zimmetli bir varlığın durumunu değiştirmek için önce iade alınız.");
        a.Name = req.Name.Trim(); a.AssetTag = tag; a.Type = req.Type; a.Status = req.Status; a.Manufacturer = req.Manufacturer?.Trim(); a.Model = req.Model?.Trim();
        a.SerialNumber = req.SerialNumber?.Trim(); a.DepartmentId = req.DepartmentId; a.Location = req.Location?.Trim(); a.PurchaseDate = req.PurchaseDate;
        a.WarrantyEndsAt = req.WarrantyEndsAt; a.PurchaseCost = req.PurchaseCost; a.Notes = req.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        return await db.Assets.AsNoTracking().Where(x => x.Id == a.Id).Select(Map).FirstAsync(ct);
    }

    /// <summary>Zimmet (teslim): varlığı kullanıcıya atar ve geçmişe kayıt düşer.</summary>
    public async Task<AssetDto> CheckoutAsync(int id, CheckoutRequest req, UserContext me, CancellationToken ct)
    {
        var a = await db.Assets.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Varlık");
        if (a.AssignedToId is not null) throw AppException.BadRequest("Varlık zaten bir kullanıcıya zimmetli. Önce iade alınız.");
        if (a.Status is AssetStatus.Retired or AssetStatus.InRepair) throw AppException.BadRequest("Hurdaya ayrılmış veya serviste olan varlık zimmetlenemez.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId && u.IsActive, ct) ?? throw AppException.BadRequest("Kullanıcı bulunamadı veya pasif.");
        a.AssignedToId = user.Id;
        a.Status = AssetStatus.InUse;
        a.DepartmentId ??= user.DepartmentId;
        db.AssetAssignments.Add(new AssetAssignment { AssetId = a.Id, UserId = user.Id, AssignedById = me.Id, AssignedAt = Now, Note = req.Note?.Trim() });
        events.Enqueue(new DirectNotificationEvent([user.Id], "Size cihaz zimmetlendi", $"{a.AssetTag} · {a.Name} adınıza zimmetlendi.", "info", $"/inventory/{a.Id}") { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Zimmet: {AssetTag} → {UserId}", a.AssetTag, user.Id);
        return await db.Assets.AsNoTracking().Where(x => x.Id == id).Select(Map).FirstAsync(ct);
    }

    /// <summary>İade: açık zimmet kaydını kapatır.</summary>
    public async Task<AssetDto> CheckinAsync(int id, CheckinRequest req, UserContext me, CancellationToken ct)
    {
        var a = await db.Assets.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Varlık");
        if (a.AssignedToId is null) throw AppException.BadRequest("Varlık kimseye zimmetli değil.");
        var open = await db.AssetAssignments.Where(x => x.AssetId == id && x.ReturnedAt == null).OrderByDescending(x => x.AssignedAt).FirstOrDefaultAsync(ct);
        if (open is not null)
        {
            open.ReturnedAt = Now;
            open.ReturnedById = me.Id;
            open.ReturnCondition = req.Condition?.Trim();
            if (!string.IsNullOrWhiteSpace(req.Note)) open.Note = $"{open.Note} | İade: {req.Note.Trim()}".Trim(' ', '|');
        }
        a.AssignedToId = null;
        a.Status = req.NewStatus is AssetStatus.InUse ? AssetStatus.InStock : req.NewStatus;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("İade: {AssetTag}", a.AssetTag);
        return await db.Assets.AsNoTracking().Where(x => x.Id == id).Select(Map).FirstAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var a = await db.Assets.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Varlık");
        if (a.AssignedToId is not null) throw AppException.BadRequest("Zimmetli varlık silinemez.");
        db.Assets.Remove(a);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<LicenseDto>> LicensesAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(Now);
        return db.SoftwareLicenses.AsNoTracking().OrderBy(l => l.Name).Select(l => new LicenseDto(l.Id, l.Name, l.Vendor, l.Version, l.Seats, l.Installations.Count,
            l.ExpiresAt, l.Cost, l.Notes, l.ExpiresAt != null && l.ExpiresAt < today)).ToListAsync(ct);
    }

    public async Task<LicenseDto> SaveLicenseAsync(int? id, SaveLicenseRequest req, CancellationToken ct)
    {
        SoftwareLicense l;
        if (id is { } e) l = await db.SoftwareLicenses.FirstOrDefaultAsync(x => x.Id == e, ct) ?? throw AppException.NotFound("Lisans");
        else db.SoftwareLicenses.Add(l = new SoftwareLicense { Name = req.Name });
        var used = id is null ? 0 : await db.SoftwareInstallations.CountAsync(i => i.LicenseId == id, ct);
        if (req.Seats < used) throw AppException.BadRequest($"Lisans adedi kullanımdaki kurulum sayısından ({used}) az olamaz.");
        l.Name = req.Name.Trim(); l.Vendor = req.Vendor?.Trim(); l.Version = req.Version?.Trim(); l.Seats = req.Seats; l.ExpiresAt = req.ExpiresAt; l.Cost = req.Cost; l.Notes = req.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        return (await LicensesAsync(ct)).First(x => x.Id == l.Id);
    }

    public async Task InstallAsync(int licenseId, int assetId, CancellationToken ct)
    {
        var l = await db.SoftwareLicenses.Include(x => x.Installations).FirstOrDefaultAsync(x => x.Id == licenseId, ct) ?? throw AppException.NotFound("Lisans");
        if (l.Installations.Any(i => i.AssetId == assetId)) throw AppException.Conflict("Bu yazılım cihazda zaten kurulu.");
        if (l.Installations.Count >= l.Seats) throw AppException.BadRequest("Lisans adedi doldu. Yeni kurulum için lisans artırınız.");
        if (!await db.Assets.AnyAsync(a => a.Id == assetId, ct)) throw AppException.NotFound("Varlık");
        db.SoftwareInstallations.Add(new SoftwareInstallation { LicenseId = licenseId, AssetId = assetId, InstalledAt = Now });
        await db.SaveChangesAsync(ct);
    }

    public Task UninstallAsync(long installationId, CancellationToken ct) => db.SoftwareInstallations.Where(i => i.Id == installationId).ExecuteDeleteAsync(ct);
}

[Route("api/assets")]
public class AssetsController(InventoryService inventory) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AssetQuery q, CancellationToken ct) => Ok(await inventory.ListAsync(q, Me, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await inventory.GetAsync(id, Me, ct));

    [HasPermission(Permissions.InventoryManage), HttpPost]
    public async Task<IActionResult> Create(SaveAssetRequest req, CancellationToken ct) => Created(await inventory.SaveAsync(null, req, ct), "Varlık eklendi.");

    [HasPermission(Permissions.InventoryManage), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveAssetRequest req, CancellationToken ct) => Ok(await inventory.SaveAsync(id, req, ct), "Varlık güncellendi.");

    [HasPermission(Permissions.InventoryManage), HttpPost("{id:int}/checkout")]
    public async Task<IActionResult> Checkout(int id, CheckoutRequest req, CancellationToken ct) => Ok(await inventory.CheckoutAsync(id, req, Me, ct), "Zimmet kaydı oluşturuldu.");

    [HasPermission(Permissions.InventoryManage), HttpPost("{id:int}/checkin")]
    public async Task<IActionResult> Checkin(int id, CheckinRequest req, CancellationToken ct) => Ok(await inventory.CheckinAsync(id, req, Me, ct), "İade alındı.");

    [HasPermission(Permissions.InventoryManage), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await inventory.DeleteAsync(id, ct); return NoContent(); }
}

[Route("api/licenses")]
[HasPermission(Permissions.InventoryView)]
public class LicensesController(InventoryService inventory) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await inventory.LicensesAsync(ct));

    [HasPermission(Permissions.InventoryManage), HttpPost]
    public async Task<IActionResult> Create(SaveLicenseRequest req, CancellationToken ct) => Created(await inventory.SaveLicenseAsync(null, req, ct), "Lisans eklendi.");

    [HasPermission(Permissions.InventoryManage), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveLicenseRequest req, CancellationToken ct) => Ok(await inventory.SaveLicenseAsync(id, req, ct), "Lisans güncellendi.");

    [HasPermission(Permissions.InventoryManage), HttpPost("{id:int}/install")]
    public async Task<IActionResult> Install(int id, InstallRequest req, CancellationToken ct) { await inventory.InstallAsync(id, req.AssetId, ct); return NoContent(); }

    [HasPermission(Permissions.InventoryManage), HttpDelete("installations/{installationId:long}")]
    public async Task<IActionResult> Uninstall(long installationId, CancellationToken ct) { await inventory.UninstallAsync(installationId, ct); return NoContent(); }
}
