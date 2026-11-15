using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

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
