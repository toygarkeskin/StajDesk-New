using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

[Route("api/audit-logs")]
public class AuditController(AppDbContext db) : ApiController
{
    [HasPermission(Permissions.AuditView), HttpGet]
    public async Task<IActionResult> List([FromQuery] AuditQuery q, CancellationToken ct)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.EntityType)) query = query.Where(a => a.EntityType == q.EntityType);
        if (!string.IsNullOrWhiteSpace(q.Action)) query = query.Where(a => a.Action == q.Action);
        if (q.UserId is { } u) query = query.Where(a => a.UserId == u);
        if (q.From is { } f) query = query.Where(a => a.CreatedAt >= f.ToUniversalTime());
        if (q.To is { } t) query = query.Where(a => a.CreatedAt <= t.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(a => (a.UserName != null && EF.Functions.ILike(a.UserName, q.Search.Like())) || a.EntityId == q.Search);
        return Ok(await query.OrderByDescending(a => a.Id).ToPagedAsync(q, a => new AuditLogDto(a.Id, a.UserId, a.UserName, a.Action, a.EntityType, a.EntityId, a.Changes, a.IpAddress, a.CreatedAt), ct));
    }

    [HasPermission(Permissions.AuditView), HttpGet("entity-types")]
    public async Task<IActionResult> EntityTypes(CancellationToken ct) => Ok(await db.AuditLogs.Select(a => a.EntityType).Distinct().OrderBy(x => x).ToListAsync(ct));
}
