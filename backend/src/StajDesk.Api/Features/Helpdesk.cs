using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

// ============================================================ DTO'lar
public record UserRef(Guid Id, string FullName, string Email);
public record FileDto(Guid Id, string FileName, string ContentType, long Size, DateTime CreatedAt);

public record TicketListItem(int Id, string Title, TicketStatus Status, TicketPriority Priority, TicketType Type, int CategoryId, string CategoryName, string CategoryColor,
    UserRef Requester, string? DepartmentName, UserRef? Assignee, string? GroupName, DateTime CreatedAt, DateTime UpdatedAt, DateTime DueAt, bool IsOverdue, int CommentCount, int AttachmentCount);

public record TicketComment(int Id, string Body, bool IsInternal, UserRef Author, bool AuthorIsStaff, DateTime CreatedAt);
public record TicketHistoryDto(long Id, string Field, string? OldValue, string? NewValue, UserRef? Actor, DateTime CreatedAt);
public record AssetRef(int Id, string Name, string AssetTag, AssetType Type);

public record TicketDetail(int Id, string Title, string Description, TicketStatus Status, TicketPriority Priority, TicketType Type,
    int CategoryId, string CategoryName, string CategoryColor, UserRef Requester, string? RequesterTitle, string? RequesterPhone, DeptRef? Department,
    int? GroupId, string? GroupName, UserRef? Assignee, AssetRef? Asset, string? AppliedRule,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime DueAt, DateTime? ResolvedAt, DateTime? ClosedAt, bool IsOverdue,
    IReadOnlyList<TicketStatus> AllowedTransitions, bool CanEdit, bool CanManage,
    IReadOnlyList<TicketComment> Comments, IReadOnlyList<TicketHistoryDto> History, IReadOnlyList<FileDto> Attachments);

public record CreateTicketRequest(string Title, string Description, int CategoryId, TicketPriority Priority, TicketType Type, int? AssetId, List<Guid>? AttachmentIds);
public record UpdateTicketRequest(string Title, string Description, int CategoryId, TicketPriority Priority, TicketType Type, int? AssetId);
public record ChangeStatusRequest(TicketStatus Status, string? Note);
public record AssignTicketRequest(int? GroupId, Guid? AssigneeId);
public record AddCommentRequest(string Body, bool IsInternal);

public class TicketQuery : PagedQuery
{
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
    public int? CategoryId { get; set; }
    public int? DepartmentId { get; set; }
    public int? GroupId { get; set; }
    public Guid? AssigneeId { get; set; }
    public int? AssetId { get; set; }
    /// <summary>mine | assigned | mygroups | unassigned | overdue | active | department</summary>
    public string? Scope { get; set; }
    /// <summary>newest | oldest | priority | due | updated</summary>
    public string? Sort { get; set; }
}

public record CategoryDto(int Id, string Name, string? Description, string Color, bool IsActive, int? DefaultGroupId, string? DefaultGroupName, int TicketCount, int OpenTicketCount);
public record SaveCategoryRequest(string Name, string? Description, string Color, bool IsActive, int? DefaultGroupId);

public record RoutingRuleDto(int Id, string Name, int SortOrder, bool IsActive, int? CategoryId, string? CategoryName, int? DepartmentId, string? DepartmentName,
    string? Keyword, TicketPriority? MinPriority, int? AssignGroupId, string? AssignGroupName, Guid? AssignUserId, string? AssignUserName, TicketPriority? SetPriority, int MatchCount);
public record SaveRoutingRuleRequest(string Name, int SortOrder, bool IsActive, int? CategoryId, int? DepartmentId, string? Keyword, TicketPriority? MinPriority,
    int? AssignGroupId, Guid? AssignUserId, TicketPriority? SetPriority);
public record TestRoutingRequest(int CategoryId, int? DepartmentId, string Title, string Description, TicketPriority Priority);

public record ArticleListItem(int Id, string Title, string Summary, int? CategoryId, string? CategoryName, string? CategoryColor, bool IsFaq, UserRef Author, int Views, int HelpfulCount, bool IsPublished, DateTime UpdatedAt);
public record ArticleDto(int Id, string Title, string Summary, string Content, int? CategoryId, string? CategoryName, string? CategoryColor, bool IsFaq, UserRef Author, int Views, int HelpfulCount, bool IsPublished, DateTime CreatedAt, DateTime UpdatedAt);
public record SaveArticleRequest(string Title, string Summary, string Content, int? CategoryId, bool IsFaq, bool IsPublished = true);
public class ArticleQuery : PagedQuery
{
    public int? CategoryId { get; set; }
    public bool? Faq { get; set; }
}

// ============================================================ Doğrulayıcılar
public class CreateTicketValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlık zorunludur.").Length(5, 200).WithMessage("Başlık 5-200 karakter olmalıdır.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Açıklama zorunludur.").Length(10, 8000).WithMessage("Açıklama en az 10 karakter olmalıdır.");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Kategori seçiniz.");
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.AttachmentIds).Must(a => a is null || a.Count <= 5).WithMessage("En fazla 5 dosya eklenebilir.");
    }
}

public class UpdateTicketValidator : AbstractValidator<UpdateTicketRequest>
{
    public UpdateTicketValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(5, 200).WithMessage("Başlık 5-200 karakter olmalıdır.");
        RuleFor(x => x.Description).NotEmpty().Length(10, 8000).WithMessage("Açıklama en az 10 karakter olmalıdır.");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Kategori seçiniz.");
    }
}

public class AddCommentValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentValidator() => RuleFor(x => x.Body).NotEmpty().WithMessage("Yorum boş olamaz.").MaximumLength(4000);
}

public class SaveCategoryValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Kategori adı zorunludur.").MaximumLength(80);
        RuleFor(x => x.Color).Matches("^#[0-9a-fA-F]{6}$").WithMessage("Renk #RRGGBB formatında olmalıdır.");
    }
}

public class SaveRoutingRuleValidator : AbstractValidator<SaveRoutingRuleRequest>
{
    public SaveRoutingRuleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Kural adı zorunludur.").MaximumLength(120);
        RuleFor(x => x).Must(x => x.AssignGroupId.HasValue || x.AssignUserId.HasValue || x.SetPriority.HasValue)
            .WithMessage("Kural en az bir eylem içermelidir (grup, kişi veya öncelik).").OverridePropertyName("assignGroupId");
    }
}

public class SaveArticleValidator : AbstractValidator<SaveArticleRequest>
{
    public SaveArticleValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(5, 200).WithMessage("Başlık 5-200 karakter olmalıdır.");
        RuleFor(x => x.Summary).NotEmpty().Length(10, 400).WithMessage("Özet 10-400 karakter olmalıdır.");
        RuleFor(x => x.Content).NotEmpty().Length(20, 20000).WithMessage("İçerik en az 20 karakter olmalıdır.");
    }
}

// ============================================================ Talep servisi
public interface ITicketService
{
    Task<PagedResult<TicketListItem>> ListAsync(TicketQuery q, UserContext me, CancellationToken ct = default);
    Task<TicketDetail> GetAsync(int id, UserContext me, CancellationToken ct = default);
    Task<TicketDetail> CreateAsync(CreateTicketRequest req, UserContext me, CancellationToken ct = default);
    Task<TicketDetail> UpdateAsync(int id, UpdateTicketRequest req, UserContext me, CancellationToken ct = default);
    Task<TicketDetail> ChangeStatusAsync(int id, ChangeStatusRequest req, UserContext me, CancellationToken ct = default);
    Task<TicketDetail> AssignAsync(int id, AssignTicketRequest req, UserContext me, CancellationToken ct = default);
    Task<TicketComment> AddCommentAsync(int id, AddCommentRequest req, UserContext me, CancellationToken ct = default);
    Task<IReadOnlyList<FileDto>> AttachAsync(int id, IReadOnlyList<Guid> fileIds, UserContext me, CancellationToken ct = default);
    Task DeleteAsync(int id, UserContext me, CancellationToken ct = default);
    Task<Expression<Func<Ticket, bool>>> VisibilityAsync(UserContext me, CancellationToken ct = default);
}

public class TicketService(AppDbContext db, IEventBus events, ICacheService cache, AccessScope scope, TimeProvider clock, ILogger<TicketService> logger) : ITicketService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public static Expression<Func<Ticket, TicketListItem>> Projection(DateTime now) => t => new TicketListItem(
        t.Id, t.Title, t.Status, t.Priority, t.Type, t.CategoryId, t.Category.Name, t.Category.Color,
        new UserRef(t.Requester.Id, t.Requester.FirstName + " " + t.Requester.LastName, t.Requester.Email),
        t.Department == null ? null : t.Department.Name,
        t.Assignee == null ? null : new UserRef(t.Assignee.Id, t.Assignee.FirstName + " " + t.Assignee.LastName, t.Assignee.Email),
        t.Group == null ? null : t.Group.Name,
        t.CreatedAt, t.UpdatedAt, t.DueAt,
        t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && t.DueAt < now,
        t.Comments.Count, t.Attachments.Count);

    /// <summary>
    /// Görünürlük kuralı (sunucu tarafında uygulanır): kendi talepleri, kendisine veya grubuna atananlar,
    /// departman yetkisi varsa sorumlu olduğu departmanların talepleri, tümünü görme yetkisi varsa hepsi.
    /// </summary>
    public async Task<Expression<Func<Ticket, bool>>> VisibilityAsync(UserContext me, CancellationToken ct = default)
    {
        if (me.Has(Permissions.TicketsViewAll)) return _ => true;
        var groups = await db.GroupMembers.Where(g => g.UserId == me.Id).Select(g => g.GroupId).ToListAsync(ct);
        var depts = me.Has(Permissions.TicketsViewDepartment) ? (await scope.DepartmentIdsAsync(me, ct)).ToList() : [];
        var id = me.Id;
        return t => t.RequesterId == id || t.AssigneeId == id || (t.GroupId != null && groups.Contains(t.GroupId.Value))
                    || (t.DepartmentId != null && depts.Contains(t.DepartmentId.Value));
    }

    private async Task<bool> CanManageAsync(Ticket t, UserContext me, CancellationToken ct)
    {
        if (!me.Has(Permissions.TicketsManage)) return false;
        if (me.Has(Permissions.TicketsViewAll)) return true;
        return t.AssigneeId == me.Id || (t.GroupId is { } g && await db.GroupMembers.AnyAsync(m => m.GroupId == g && m.UserId == me.Id, ct));
    }

    public async Task<PagedResult<TicketListItem>> ListAsync(TicketQuery q, UserContext me, CancellationToken ct = default)
    {
        var now = Now;
        var query = db.Tickets.AsNoTracking().Where(await VisibilityAsync(me, ct));
        if (q.Status is { } s) query = query.Where(t => t.Status == s);
        if (q.Priority is { } p) query = query.Where(t => t.Priority == p);
        if (q.CategoryId is { } c) query = query.Where(t => t.CategoryId == c);
        if (q.DepartmentId is { } d) query = query.Where(t => t.DepartmentId == d);
        if (q.GroupId is { } g) query = query.Where(t => t.GroupId == g);
        if (q.AssigneeId is { } a) query = query.Where(t => t.AssigneeId == a);
        if (q.AssetId is { } asset) query = query.Where(t => t.AssetId == asset);

        var myGroups = q.Scope == "mygroups" ? await db.GroupMembers.Where(m => m.UserId == me.Id).Select(m => (int?)m.GroupId).ToListAsync(ct) : [];
        query = q.Scope switch
        {
            "mine" => query.Where(t => t.RequesterId == me.Id),
            "assigned" => query.Where(t => t.AssigneeId == me.Id),
            "mygroups" => query.Where(t => myGroups.Contains(t.GroupId)),
            "unassigned" => query.Where(t => t.AssigneeId == null && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved),
            "overdue" => query.Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed && t.DueAt < now),
            "active" => query.Where(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed),
            "department" => me.DepartmentId is { } md ? query.Where(t => t.DepartmentId == md) : query,
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = int.TryParse(term.TrimStart('#'), out var idSearch)
                ? query.Where(t => t.Id == idSearch)
                : query.Where(t => EF.Functions.ILike(t.Title, term.Like()) || EF.Functions.ILike(t.Description, term.Like()));
        }

        query = q.Sort switch
        {
            "oldest" => query.OrderBy(t => t.CreatedAt),
            "priority" => query.OrderByDescending(t => t.Priority == TicketPriority.Critical ? 3 : t.Priority == TicketPriority.High ? 2 : t.Priority == TicketPriority.Medium ? 1 : 0).ThenByDescending(t => t.CreatedAt),
            "due" => query.OrderBy(t => t.DueAt),
            "updated" => query.OrderByDescending(t => t.UpdatedAt),
            _ => query.OrderByDescending(t => t.CreatedAt),
        };
        return await query.ToPagedAsync(q, Projection(now), ct);
    }

    public async Task<TicketDetail> GetAsync(int id, UserContext me, CancellationToken ct = default)
    {
        var visible = await VisibilityAsync(me, ct);
        var t = await db.Tickets.AsNoTracking().Where(x => x.Id == id)
            .Include(x => x.Category).Include(x => x.Requester).ThenInclude(r => r.JobTitle).Include(x => x.Department)
            .Include(x => x.Group).Include(x => x.Assignee).Include(x => x.Asset)
            .Include(x => x.Comments).ThenInclude(c => c.Author).ThenInclude(a => a.Roles).ThenInclude(r => r.Role)
            .Include(x => x.History).ThenInclude(h => h.Actor)
            .Include(x => x.Attachments).ThenInclude(a => a.File)
            .AsSplitQuery().FirstOrDefaultAsync(ct) ?? throw AppException.NotFound("Talep");
        if (!await db.Tickets.Where(x => x.Id == id).AnyAsync(visible, ct))
            throw AppException.Forbidden("Bu talebi görüntüleme yetkiniz bulunmamaktadır.");

        var canManage = await CanManageAsync(t, me, ct);
        var rule = t.AppliedRuleId is { } r ? await db.RoutingRules.Where(x => x.Id == r).Select(x => x.Name).FirstOrDefaultAsync(ct) : null;
        var isStaff = me.Has(Permissions.TicketsManage);
        var transitions = canManage ? TicketRules.AllowedNext(t.Status)
            : t.RequesterId == me.Id ? TicketRules.AllowedNext(t.Status).Where(s => TicketRules.RequesterCanTransition(t.Status, s)).ToList() : [];

        return new TicketDetail(t.Id, t.Title, t.Description, t.Status, t.Priority, t.Type, t.CategoryId, t.Category.Name, t.Category.Color,
            ToRef(t.Requester), t.Requester.JobTitle?.Name, isStaff ? t.Requester.Phone : null,
            t.Department is null ? null : new DeptRef(t.Department.Id, t.Department.Name), t.GroupId, t.Group?.Name,
            t.Assignee is null ? null : ToRef(t.Assignee), t.Asset is null ? null : new AssetRef(t.Asset.Id, t.Asset.Name, t.Asset.AssetTag, t.Asset.Type), rule,
            t.CreatedAt, t.UpdatedAt, t.DueAt, t.ResolvedAt, t.ClosedAt, t.IsOverdue(Now), transitions, CanEdit(t, me, canManage), canManage,
            t.Comments.Where(c => isStaff || !c.IsInternal).OrderBy(c => c.CreatedAt)
                .Select(c => new TicketComment(c.Id, c.Body, c.IsInternal, ToRef(c.Author), c.Author.Roles.Any(r => r.Role.Permissions.Contains(Permissions.TicketsManage)), c.CreatedAt)).ToList(),
            t.History.OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
                .Select(h => new TicketHistoryDto(h.Id, h.Field, h.OldValue, h.NewValue, h.Actor is null ? null : ToRef(h.Actor), h.CreatedAt)).ToList(),
            t.Attachments.Select(a => new FileDto(a.File.Id, a.File.FileName, a.File.ContentType, a.File.Size, a.File.CreatedAt)).ToList());
    }

    public async Task<TicketDetail> CreateAsync(CreateTicketRequest req, UserContext me, CancellationToken ct = default)
    {
        var category = await db.TicketCategories.FirstOrDefaultAsync(c => c.Id == req.CategoryId && c.IsActive, ct)
                       ?? throw AppException.BadRequest("Seçilen kategori bulunamadı veya pasif.");
        await EnsureAssetUsable(req.AssetId, me, ct);
        var requester = await db.Users.FirstAsync(u => u.Id == me.Id, ct);

        var now = Now;
        var ticket = new Ticket
        {
            Title = req.Title.Trim(), Description = req.Description.Trim(), CategoryId = category.Id, Priority = req.Priority, Type = req.Type,
            AssetId = req.AssetId, RequesterId = me.Id, DepartmentId = requester.DepartmentId, GroupId = category.DefaultGroupId, CreatedAt = now, UpdatedAt = now,
        };
        ticket.History.Add(new TicketHistory { Field = "created", NewValue = TicketRules.Label(TicketStatus.Open), ActorId = me.Id, CreatedAt = now });

        // GLPI benzeri iş kuralları: sıra numarasına göre ilk eşleşen kural uygulanır.
        var rules = await db.RoutingRules.Where(r => r.IsActive).OrderBy(r => r.SortOrder).ToListAsync(ct);
        if (rules.FirstOrDefault(r => r.Matches(ticket.CategoryId, ticket.DepartmentId, ticket.Title, ticket.Description, ticket.Priority)) is { } rule)
        {
            ticket.AppliedRuleId = rule.Id;
            rule.MatchCount++;
            if (rule.SetPriority is { } p && p != ticket.Priority)
            {
                ticket.History.Add(new TicketHistory { Field = "priority", OldValue = TicketRules.Label(ticket.Priority), NewValue = TicketRules.Label(p), CreatedAt = now });
                ticket.Priority = p;
            }
            if (rule.AssignGroupId is { } g) ticket.GroupId = g;
            if (rule.AssignUserId is { } u && await db.Users.AnyAsync(x => x.Id == u && x.IsActive, ct))
            {
                ticket.AssigneeId = u;
                ticket.History.Add(new TicketHistory { Field = "assignee", NewValue = await db.Users.Where(x => x.Id == u).Select(x => x.FirstName + " " + x.LastName).FirstAsync(ct), CreatedAt = now });
            }
            ticket.History.Add(new TicketHistory { Field = "rule", NewValue = rule.Name, CreatedAt = now });
            logger.LogInformation("Yönlendirme kuralı uygulandı: {Rule}", rule.Name);
        }
        ticket.DueAt = now + TicketRules.SlaFor(ticket.Priority);

        if (req.AttachmentIds is { Count: > 0 } files)
        {
            var owned = await db.Files.Where(f => files.Contains(f.Id) && f.UploadedById == me.Id).Select(f => f.Id).ToListAsync(ct);
            foreach (var f in owned) ticket.Attachments.Add(new TicketAttachment { FileId = f });
        }

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);
        events.Enqueue(new TicketEvent("created", ticket.Id, ticket.Title, ticket.Priority.ToString(), me.Id, ticket.AssigneeId, ticket.GroupId) { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Talep oluşturuldu #{TicketId} öncelik {Priority}", ticket.Id, ticket.Priority);
        await InvalidateStats();
        return await GetAsync(ticket.Id, me, ct);
    }

    public async Task<TicketDetail> UpdateAsync(int id, UpdateTicketRequest req, UserContext me, CancellationToken ct = default)
    {
        var t = await LoadVisibleAsync(id, me, ct);
        await db.Entry(t).Reference(x => x.Category).LoadAsync(ct);
        var canManage = await CanManageAsync(t, me, ct);
        if (!CanEdit(t, me, canManage)) throw AppException.Forbidden("Talep bu aşamada düzenlenemez.");

        if (t.CategoryId != req.CategoryId)
        {
            var c = await db.TicketCategories.FirstOrDefaultAsync(x => x.Id == req.CategoryId && x.IsActive, ct) ?? throw AppException.BadRequest("Seçilen kategori bulunamadı veya pasif.");
            Track(t, "category", t.Category.Name, c.Name, me);
            t.CategoryId = c.Id;
        }
        if (t.Priority != req.Priority)
        {
            Track(t, "priority", TicketRules.Label(t.Priority), TicketRules.Label(req.Priority), me);
            t.Priority = req.Priority;
            t.DueAt = t.CreatedAt + TicketRules.SlaFor(req.Priority);
        }
        if (t.AssetId != req.AssetId) { await EnsureAssetUsable(req.AssetId, me, ct); t.AssetId = req.AssetId; }
        if (t.Title != req.Title.Trim()) Track(t, "title", t.Title, req.Title.Trim(), me);
        t.Title = req.Title.Trim();
        t.Description = req.Description.Trim();
        t.Type = req.Type;
        await db.SaveChangesAsync(ct);
        await InvalidateStats();
        return await GetAsync(id, me, ct);
    }

    public async Task<TicketDetail> ChangeStatusAsync(int id, ChangeStatusRequest req, UserContext me, CancellationToken ct = default)
    {
        var t = await LoadVisibleAsync(id, me, ct);
        var from = t.Status;
        if (from == req.Status) throw AppException.BadRequest("Talep zaten bu durumda.");
        var canManage = await CanManageAsync(t, me, ct);
        var allowed = canManage ? TicketRules.CanTransition(from, req.Status)
            : t.RequesterId == me.Id && TicketRules.RequesterCanTransition(from, req.Status);
        if (!allowed)
            throw AppException.BadRequest($"'{TicketRules.Label(from)}' durumundaki talep '{TicketRules.Label(req.Status)}' durumuna geçirilemez.");
        if (req.Status == TicketStatus.Resolved && canManage && string.IsNullOrWhiteSpace(req.Note) && !await db.TicketComments.AnyAsync(c => c.TicketId == id && !c.IsInternal, ct))
            throw AppException.BadRequest("Talebi çözüldü olarak işaretlemeden önce kullanıcıya bir çözüm açıklaması yazınız.");

        var now = Now;
        t.Status = req.Status;
        switch (req.Status)
        {
            case TicketStatus.Resolved: t.ResolvedAt = now; break;
            case TicketStatus.Closed: t.ClosedAt = now; t.ResolvedAt ??= now; break;
            default: t.ResolvedAt = null; break;
        }
        if (req.Status == TicketStatus.InProgress && t.AssigneeId is null && canManage)
        {
            t.AssigneeId = me.Id;
            Track(t, "assignee", null, me.Name, me);
        }
        Track(t, "status", TicketRules.Label(from), TicketRules.Label(req.Status), me);
        if (!string.IsNullOrWhiteSpace(req.Note)) t.Comments.Add(new Domain.TicketComment { Body = req.Note.Trim(), AuthorId = me.Id, CreatedAt = now });

        events.Enqueue(new TicketEvent("status_changed", t.Id, t.Title, t.Priority.ToString(), t.RequesterId, t.AssigneeId, t.GroupId, from.ToString(), req.Status.ToString()) { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Talep #{TicketId} durumu {From} -> {To}", t.Id, from, req.Status);
        await InvalidateStats();
        return await GetAsync(id, me, ct);
    }

    public async Task<TicketDetail> AssignAsync(int id, AssignTicketRequest req, UserContext me, CancellationToken ct = default)
    {
        var t = await LoadVisibleAsync(id, me, ct);
        if (!await CanManageAsync(t, me, ct)) throw AppException.Forbidden("Bu talebi atama yetkiniz bulunmamaktadır.");
        if (t.Status == TicketStatus.Closed) throw AppException.BadRequest("Kapanmış talep yeniden atanamaz.");
        await db.Entry(t).Reference(x => x.Assignee).LoadAsync(ct);
        await db.Entry(t).Reference(x => x.Group).LoadAsync(ct);

        if (req.GroupId != t.GroupId)
        {
            var g = req.GroupId is { } gid ? await db.Groups.FirstOrDefaultAsync(x => x.Id == gid, ct) ?? throw AppException.NotFound("Grup") : null;
            Track(t, "group", t.Group?.Name, g?.Name, me);
            t.GroupId = g?.Id;
        }
        if (req.AssigneeId != t.AssigneeId)
        {
            User? assignee = null;
            if (req.AssigneeId is { } aid)
            {
                assignee = await db.Users.Include(u => u.Roles).ThenInclude(r => r.Role).FirstOrDefaultAsync(u => u.Id == aid, ct) ?? throw AppException.NotFound("Personel");
                if (!assignee.IsActive || !assignee.Roles.Any(r => r.Role.Permissions.Contains(Permissions.TicketsManage)))
                    throw AppException.BadRequest("Talep yalnızca aktif destek personeline atanabilir.");
            }
            Track(t, "assignee", t.Assignee?.FullName, assignee?.FullName, me);
            t.AssigneeId = assignee?.Id;
            events.Enqueue(new TicketEvent("assigned", t.Id, t.Title, t.Priority.ToString(), t.RequesterId, t.AssigneeId, t.GroupId, null, assignee?.FullName) { ActorId = me.Id, ActorName = me.Name });
        }
        await db.SaveChangesAsync(ct);
        await InvalidateStats();
        return await GetAsync(id, me, ct);
    }

    public async Task<TicketComment> AddCommentAsync(int id, AddCommentRequest req, UserContext me, CancellationToken ct = default)
    {
        var t = await LoadVisibleAsync(id, me, ct);
        if (t.Status == TicketStatus.Closed) throw AppException.BadRequest("Kapanmış talebe yorum eklenemez.");
        var isStaff = me.Has(Permissions.TicketsManage);
        if (req.IsInternal && !isStaff) throw AppException.Forbidden("İç notlar yalnızca destek personeli tarafından eklenebilir.");

        var comment = new Domain.TicketComment { TicketId = t.Id, AuthorId = me.Id, Body = req.Body.Trim(), IsInternal = req.IsInternal, CreatedAt = Now };
        db.TicketComments.Add(comment);
        t.UpdatedAt = Now;
        // Kullanıcı beklemedeki talebe yanıt verirse talep tekrar işleme alınır (GLPI davranışı)
        if (t.Status == TicketStatus.Pending && t.RequesterId == me.Id)
        {
            Track(t, "status", TicketRules.Label(TicketStatus.Pending), TicketRules.Label(TicketStatus.InProgress), me);
            t.Status = TicketStatus.InProgress;
        }
        events.Enqueue(new TicketEvent("commented", t.Id, t.Title, t.Priority.ToString(), t.RequesterId, t.AssigneeId, t.GroupId, IsInternal: req.IsInternal) { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        var author = await db.Users.AsNoTracking().FirstAsync(u => u.Id == me.Id, ct);
        return new TicketComment(comment.Id, comment.Body, comment.IsInternal, ToRef(author), isStaff, comment.CreatedAt);
    }

    public async Task<IReadOnlyList<FileDto>> AttachAsync(int id, IReadOnlyList<Guid> fileIds, UserContext me, CancellationToken ct = default)
    {
        var t = await LoadVisibleAsync(id, me, ct);
        if (t.Status == TicketStatus.Closed) throw AppException.BadRequest("Kapanmış talebe dosya eklenemez.");
        var count = await db.TicketAttachments.CountAsync(a => a.TicketId == id, ct);
        if (count + fileIds.Count > 10) throw AppException.BadRequest("Bir talebe en fazla 10 dosya eklenebilir.");
        foreach (var f in fileIds) db.TicketAttachments.Add(new TicketAttachment { TicketId = id, FileId = f });
        Track(t, "attachment", null, $"{fileIds.Count} dosya", me);
        await db.SaveChangesAsync(ct);
        return await db.TicketAttachments.Where(a => a.TicketId == id).Select(a => new FileDto(a.File.Id, a.File.FileName, a.File.ContentType, a.File.Size, a.File.CreatedAt)).ToListAsync(ct);
    }

    public async Task DeleteAsync(int id, UserContext me, CancellationToken ct = default)
    {
        if (!me.Has(Permissions.TicketsDelete)) throw AppException.Forbidden();
        var t = await db.Tickets.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Talep");
        db.Tickets.Remove(t);
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Talep #{TicketId} silindi (işlemi yapan {ActorId})", id, me.Id);
        await InvalidateStats();
    }

    // ---------- yardımcılar ----------
    private async Task<Ticket> LoadVisibleAsync(int id, UserContext me, CancellationToken ct)
    {
        var t = await db.Tickets.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Talep");
        if (!await db.Tickets.Where(x => x.Id == id).AnyAsync(await VisibilityAsync(me, ct), ct))
            throw AppException.Forbidden("Bu talebe erişim yetkiniz bulunmamaktadır.");
        return t;
    }

    private static bool CanEdit(Ticket t, UserContext me, bool canManage) =>
        t.Status != TicketStatus.Closed && (canManage || (t.RequesterId == me.Id && t.Status == TicketStatus.Open));

    private async Task EnsureAssetUsable(int? assetId, UserContext me, CancellationToken ct)
    {
        if (assetId is not { } aid) return;
        var asset = await db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aid, ct) ?? throw AppException.BadRequest("Seçilen varlık bulunamadı.");
        if (!me.Has(Permissions.InventoryView) && asset.AssignedToId != me.Id && asset.DepartmentId != me.DepartmentId)
            throw AppException.BadRequest("Yalnızca size veya biriminize zimmetli bir cihaz seçebilirsiniz.");
    }

    private void Track(Ticket t, string field, string? oldValue, string? newValue, UserContext me) =>
        t.History.Add(new TicketHistory { TicketId = t.Id, Field = field, OldValue = oldValue, NewValue = newValue, ActorId = me.Id, CreatedAt = Now });

    private Task InvalidateStats() => cache.RemoveByPrefixAsync(CacheKeys.StatsPrefix);

    private static UserRef ToRef(User u) => new(u.Id, u.FullName, u.Email);
}

// ============================================================ Yapılandırma servisleri
public class HelpdeskConfigService(AppDbContext db, ICacheService cache)
{
    public Task<List<CategoryDto>> CategoriesAsync(CancellationToken ct) =>
        cache.GetOrSetAsync(CacheKeys.Categories, () => db.TicketCategories.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Color, c.IsActive, c.DefaultGroupId, c.DefaultGroup == null ? null : c.DefaultGroup.Name,
                c.Tickets.Count, c.Tickets.Count(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed))).ToListAsync(ct), TimeSpan.FromSeconds(60));

    public async Task<CategoryDto> SaveCategoryAsync(int? id, SaveCategoryRequest req, CancellationToken ct)
    {
        var name = req.Name.Trim();
        if (await db.TicketCategories.AnyAsync(c => c.Name == name && c.Id != id, ct)) throw AppException.Conflict("Bu isimde bir kategori zaten var.");
        TicketCategory c;
        if (id is { } e) c = await db.TicketCategories.FirstOrDefaultAsync(x => x.Id == e, ct) ?? throw AppException.NotFound("Kategori");
        else db.TicketCategories.Add(c = new TicketCategory { Name = name });
        c.Name = name; c.Description = req.Description?.Trim(); c.Color = req.Color; c.IsActive = req.IsActive; c.DefaultGroupId = req.DefaultGroupId;
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Categories);
        return (await CategoriesAsync(ct)).First(x => x.Id == c.Id);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct)
    {
        if (await db.Tickets.AnyAsync(t => t.CategoryId == id, ct)) throw AppException.Conflict("Bu kategoride talepler bulunduğu için silinemez; pasif hâle getirebilirsiniz.");
        await db.TicketCategories.Where(c => c.Id == id).ExecuteDeleteAsync(ct);
        await cache.RemoveAsync(CacheKeys.Categories);
    }

    public Task<List<RoutingRuleDto>> RulesAsync(CancellationToken ct) =>
        db.RoutingRules.AsNoTracking().OrderBy(r => r.SortOrder).Select(r => new RoutingRuleDto(r.Id, r.Name, r.SortOrder, r.IsActive, r.CategoryId,
            r.Category == null ? null : r.Category.Name, r.DepartmentId, r.Department == null ? null : r.Department.Name, r.Keyword, r.MinPriority,
            r.AssignGroupId, r.AssignGroup == null ? null : r.AssignGroup.Name, r.AssignUserId, r.AssignUser == null ? null : r.AssignUser.FirstName + " " + r.AssignUser.LastName,
            r.SetPriority, r.MatchCount)).ToListAsync(ct);

    public async Task<RoutingRuleDto> SaveRuleAsync(int? id, SaveRoutingRuleRequest req, CancellationToken ct)
    {
        RoutingRule r;
        if (id is { } e) r = await db.RoutingRules.FirstOrDefaultAsync(x => x.Id == e, ct) ?? throw AppException.NotFound("Kural");
        else db.RoutingRules.Add(r = new RoutingRule { Name = req.Name });
        r.Name = req.Name.Trim(); r.SortOrder = req.SortOrder; r.IsActive = req.IsActive; r.CategoryId = req.CategoryId; r.DepartmentId = req.DepartmentId;
        r.Keyword = string.IsNullOrWhiteSpace(req.Keyword) ? null : req.Keyword.Trim(); r.MinPriority = req.MinPriority;
        r.AssignGroupId = req.AssignGroupId; r.AssignUserId = req.AssignUserId; r.SetPriority = req.SetPriority;
        await db.SaveChangesAsync(ct);
        return (await RulesAsync(ct)).First(x => x.Id == r.Id);
    }

    public Task DeleteRuleAsync(int id, CancellationToken ct) => db.RoutingRules.Where(r => r.Id == id).ExecuteDeleteAsync(ct);

    public async Task<RoutingRuleDto?> TestAsync(TestRoutingRequest req, CancellationToken ct)
    {
        var rules = await db.RoutingRules.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.SortOrder).ToListAsync(ct);
        var match = rules.FirstOrDefault(r => r.Matches(req.CategoryId, req.DepartmentId, req.Title, req.Description, req.Priority));
        return match is null ? null : (await RulesAsync(ct)).First(x => x.Id == match.Id);
    }
}

public class KnowledgeService(AppDbContext db)
{
    public Task<PagedResult<ArticleListItem>> ListAsync(ArticleQuery q, UserContext me, CancellationToken ct)
    {
        var query = db.KnowledgeArticles.AsNoTracking().AsQueryable();
        if (!me.Has(Permissions.KnowledgeManage)) query = query.Where(a => a.IsPublished);
        if (q.CategoryId is { } c) query = query.Where(a => a.CategoryId == c);
        if (q.Faq is { } f) query = query.Where(a => a.IsFaq == f);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var t = q.Search.Like();
            query = query.Where(a => EF.Functions.ILike(a.Title, t) || EF.Functions.ILike(a.Summary, t) || EF.Functions.ILike(a.Content, t));
        }
        return query.OrderByDescending(a => a.Views).ThenByDescending(a => a.UpdatedAt).ToPagedAsync(q, a => new ArticleListItem(a.Id, a.Title, a.Summary, a.CategoryId,
            a.Category == null ? null : a.Category.Name, a.Category == null ? null : a.Category.Color, a.IsFaq,
            new UserRef(a.Author.Id, a.Author.FirstName + " " + a.Author.LastName, a.Author.Email), a.Views, a.HelpfulCount, a.IsPublished, a.UpdatedAt), ct);
    }

    public async Task<ArticleDto> GetAsync(int id, UserContext me, bool countView, CancellationToken ct)
    {
        if (countView) await db.KnowledgeArticles.Where(a => a.Id == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Views, a => a.Views + 1), ct);
        var a = await db.KnowledgeArticles.AsNoTracking().Include(x => x.Category).Include(x => x.Author).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Makale");
        if (!a.IsPublished && !me.Has(Permissions.KnowledgeManage)) throw AppException.NotFound("Makale");
        return new ArticleDto(a.Id, a.Title, a.Summary, a.Content, a.CategoryId, a.Category?.Name, a.Category?.Color, a.IsFaq,
            new UserRef(a.Author.Id, a.Author.FullName, a.Author.Email), a.Views, a.HelpfulCount, a.IsPublished, a.CreatedAt, a.UpdatedAt);
    }

    public async Task<ArticleDto> SaveAsync(int? id, SaveArticleRequest req, UserContext me, CancellationToken ct)
    {
        KnowledgeArticle a;
        if (id is { } e) a = await db.KnowledgeArticles.FirstOrDefaultAsync(x => x.Id == e, ct) ?? throw AppException.NotFound("Makale");
        else db.KnowledgeArticles.Add(a = new KnowledgeArticle { Title = req.Title, Summary = req.Summary, Content = req.Content, AuthorId = me.Id });
        a.Title = req.Title.Trim(); a.Summary = req.Summary.Trim(); a.Content = req.Content.Trim(); a.CategoryId = req.CategoryId; a.IsFaq = req.IsFaq; a.IsPublished = req.IsPublished;
        await db.SaveChangesAsync(ct);
        return await GetAsync(a.Id, me, false, ct);
    }

    public async Task HelpfulAsync(int id, CancellationToken ct)
    {
        if (await db.KnowledgeArticles.Where(a => a.Id == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.HelpfulCount, a => a.HelpfulCount + 1), ct) == 0)
            throw AppException.NotFound("Makale");
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        if (await db.KnowledgeArticles.Where(a => a.Id == id).ExecuteDeleteAsync(ct) == 0) throw AppException.NotFound("Makale");
    }
}

// ============================================================ Controller'lar
[Route("api/tickets")]
public class TicketsController(ITicketService tickets) : ApiController
{
    /// <summary>Talep listesi. Sonuçlar kullanıcının yetki kapsamına göre sunucu tarafında filtrelenir.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TicketQuery q, CancellationToken ct) => Ok(await tickets.ListAsync(q, Me, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await tickets.GetAsync(id, Me, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateTicketRequest req, CancellationToken ct)
    {
        var t = await tickets.CreateAsync(req, Me, ct);
        return Created(t, $"#{t.Id} numaralı talebiniz oluşturuldu.");
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateTicketRequest req, CancellationToken ct) => Ok(await tickets.UpdateAsync(id, req, Me, ct), "Talep güncellendi.");

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, ChangeStatusRequest req, CancellationToken ct) => Ok(await tickets.ChangeStatusAsync(id, req, Me, ct), "Talep durumu güncellendi.");

    [HasPermission(Permissions.TicketsManage), HttpPatch("{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, AssignTicketRequest req, CancellationToken ct) => Ok(await tickets.AssignAsync(id, req, Me, ct), "Atama güncellendi.");

    [HttpPost("{id:int}/comments")]
    public async Task<IActionResult> Comment(int id, AddCommentRequest req, CancellationToken ct) => Created(await tickets.AddCommentAsync(id, req, Me, ct), "Yanıtınız eklendi.");

    /// <summary>Talebe dosya ekler (önce /api/files ile yüklenen dosya kimlikleri).</summary>
    [HttpPost("{id:int}/attachments")]
    public async Task<IActionResult> Attach(int id, [FromBody] List<Guid> fileIds, [FromServices] AppDbContext db, CancellationToken ct)
    {
        var owned = await db.Files.Where(f => fileIds.Contains(f.Id) && f.UploadedById == Me.Id).Select(f => f.Id).ToListAsync(ct);
        return Ok(await tickets.AttachAsync(id, owned, Me, ct), "Dosya eklendi.");
    }

    [HasPermission(Permissions.TicketsDelete), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await tickets.DeleteAsync(id, Me, ct); return NoContent(); }
}

[Route("api/ticket-categories")]
public class TicketCategoriesController(HelpdeskConfigService config) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await config.CategoriesAsync(ct));

    [HasPermission(Permissions.TicketsConfigure), HttpPost]
    public async Task<IActionResult> Create(SaveCategoryRequest req, CancellationToken ct) => Created(await config.SaveCategoryAsync(null, req, ct), "Kategori eklendi.");

    [HasPermission(Permissions.TicketsConfigure), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveCategoryRequest req, CancellationToken ct) => Ok(await config.SaveCategoryAsync(id, req, ct), "Kategori güncellendi.");

    [HasPermission(Permissions.TicketsConfigure), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await config.DeleteCategoryAsync(id, ct); return NoContent(); }
}

[Route("api/routing-rules")]
[HasPermission(Permissions.TicketsConfigure)]
public class RoutingRulesController(HelpdeskConfigService config) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await config.RulesAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(SaveRoutingRuleRequest req, CancellationToken ct) => Created(await config.SaveRuleAsync(null, req, ct), "Kural eklendi.");

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveRoutingRuleRequest req, CancellationToken ct) => Ok(await config.SaveRuleAsync(id, req, ct), "Kural güncellendi.");

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await config.DeleteRuleAsync(id, ct); return NoContent(); }

    /// <summary>Örnek bir talebin hangi kurala takılacağını gösterir.</summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test(TestRoutingRequest req, CancellationToken ct) => Ok(await config.TestAsync(req, ct));
}

[Route("api/kb")]
public class KnowledgeController(KnowledgeService kb) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ArticleQuery q, CancellationToken ct) => Ok(await kb.ListAsync(q, Me, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, [FromQuery] bool view = true, CancellationToken ct = default) => Ok(await kb.GetAsync(id, Me, view, ct));

    [HasPermission(Permissions.KnowledgeManage), HttpPost]
    public async Task<IActionResult> Create(SaveArticleRequest req, CancellationToken ct) => Created(await kb.SaveAsync(null, req, Me, ct), "Makale yayınlandı.");

    [HasPermission(Permissions.KnowledgeManage), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveArticleRequest req, CancellationToken ct) => Ok(await kb.SaveAsync(id, req, Me, ct), "Makale güncellendi.");

    [HttpPost("{id:int}/helpful")]
    public async Task<IActionResult> Helpful(int id, CancellationToken ct) { await kb.HelpfulAsync(id, ct); return NoContent(); }

    [HasPermission(Permissions.KnowledgeManage), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await kb.DeleteAsync(id, ct); return NoContent(); }
}

/// <summary>Dosya yükleme ve yetki kontrollü indirme.</summary>
[Route("api/files")]
public class FilesController(FileStorage storage, AppDbContext db, ITicketService tickets) : ApiController
{
    [HttpPost, RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        var stored = await storage.SaveAsync(file, Me.Id, ct);
        await db.SaveChangesAsync(ct);
        return Created(new FileDto(stored.Id, stored.FileName, stored.ContentType, stored.Size, stored.CreatedAt), "Dosya yüklendi.");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct) ?? throw AppException.NotFound("Dosya");
        if (!await CanReadAsync(file, ct)) throw AppException.Forbidden("Bu dosyaya erişim yetkiniz bulunmamaktadır.");
        await db.Documents.Where(d => d.FileId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Downloads, d => d.Downloads + 1), ct);
        return File(storage.Open(file), file.ContentType, file.FileName);
    }

    private async Task<bool> CanReadAsync(StoredFile file, CancellationToken ct)
    {
        var me = Me;
        if (file.UploadedById == me.Id || me.IsAdmin) return true;
        var ticketIds = await db.TicketAttachments.Where(a => a.FileId == file.Id).Select(a => a.TicketId).ToListAsync(ct);
        if (ticketIds.Count > 0 && await db.Tickets.Where(t => ticketIds.Contains(t.Id)).AnyAsync(await tickets.VisibilityAsync(me, ct), ct)) return true;
        var subs = await db.SubmissionFiles.Where(s => s.FileId == file.Id).Select(s => s.Submission).ToListAsync(ct);
        if (subs.Any(s => s.SubmitterId == me.Id || s.AssigneeId == me.Id || s.PendingApproverIds.Contains(me.Id)) || (subs.Count > 0 && me.Has(Permissions.SubmissionsViewAll))) return true;
        if (subs.Count > 0 && await db.SubmissionActions.AnyAsync(a => subs.Select(s => s.Id).Contains(a.SubmissionId) && a.ActorId == me.Id, ct)) return true;
        var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.FileId == file.Id, ct);
        return doc is not null && (doc.DepartmentId is null || doc.DepartmentId == me.DepartmentId || me.Has(Permissions.DocumentsManage));
    }
}
