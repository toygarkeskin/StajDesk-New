using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

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
