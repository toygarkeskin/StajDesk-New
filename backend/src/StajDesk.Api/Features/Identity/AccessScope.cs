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


/// <summary>Kullanıcının yönetici olarak görebileceği departmanlar: kendi departmanı, yönettiği departmanlar ve doğrudan bağlılarının departmanları.</summary>
public class AccessScope(AppDbContext db)
{
    private HashSet<int>? _cache;

    public async Task<HashSet<int>> DepartmentIdsAsync(UserContext u, CancellationToken ct)
    {
        if (_cache is not null) return _cache;
        var managed = await db.Departments.Where(d => d.ManagerId == u.Id).Select(d => d.Id).ToListAsync(ct);
        var reports = await db.Users.Where(x => x.ManagerId == u.Id && x.DepartmentId != null).Select(x => x.DepartmentId!.Value).Distinct().ToListAsync(ct);
        _cache = [.. managed, .. reports];
        if (u.DepartmentId is { } d) _cache.Add(d);
        return _cache;
    }
}
