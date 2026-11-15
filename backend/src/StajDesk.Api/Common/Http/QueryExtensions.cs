using System.Net;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Common;

public static class QueryExtensions
{
    public static async Task<PagedResult<TOut>> ToPagedAsync<TIn, TOut>(this IQueryable<TIn> q, PagedQuery p, System.Linq.Expressions.Expression<Func<TIn, TOut>> map, CancellationToken ct)
    {
        var total = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(q, ct);
        var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(q.Skip((p.Page - 1) * p.PageSize).Take(p.PageSize).Select(map), ct);
        return new PagedResult<TOut>(items, p.Page, p.PageSize, total);
    }

    public static string Like(this string term) => $"%{term.Trim().Replace("%", "\\%").Replace("_", "\\_")}%";
}
