using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

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
