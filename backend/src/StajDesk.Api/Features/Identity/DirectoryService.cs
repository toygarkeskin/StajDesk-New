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

public class DirectoryService(AppDbContext db, TimeProvider clock)
{
    public Task<PagedResult<DirectoryEntry>> SearchAsync(UserQuery q, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (q.DepartmentId is { } d) query = query.Where(u => u.DepartmentId == d);
        if (q.Ids is { Count: > 0 } ids) query = query.Where(u => ids.Contains(u.Id));
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var t = q.Search.Like();
            query = query.Where(u => EF.Functions.ILike(u.FirstName + " " + u.LastName, t) || EF.Functions.ILike(u.Email, t)
                                     || (u.JobTitle != null && EF.Functions.ILike(u.JobTitle.Name, t)) || (u.Department != null && EF.Functions.ILike(u.Department.Name, t)));
        }
        // Çalışan aramasında yalnızca iş iletişim bilgileri döner; doğum yılı, kimlik vb. özel veriler dönmez.
        return query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToPagedAsync(q, u => new DirectoryEntry(u.Id, u.FirstName + " " + u.LastName, u.Email, u.Phone,
            u.JobTitle == null ? null : u.JobTitle.Name, u.Department == null ? null : new DeptRef(u.Department.Id, u.Department.Name), u.Location,
            u.Manager == null ? null : new Ref(u.Manager.Id, u.Manager.FirstName + " " + u.Manager.LastName), u.HireDate), ct);
    }

    /// <summary>Tek çalışanın iş iletişim bilgileri (ör. form tasarımcısında seçili onaylayıcıyı göstermek için).</summary>
    public async Task<DirectoryEntry> GetAsync(Guid id, CancellationToken ct) =>
        (await SearchAsync(new UserQuery { Ids = [id], PageSize = 1 }, ct)).Items.FirstOrDefault() ?? throw AppException.NotFound("Çalışan");

    public async Task<List<BirthdayEntry>> BirthdaysAsync(int days, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var people = await db.Users.AsNoTracking().Where(u => u.IsActive && u.BirthDate != null)
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName, Title = u.JobTitle == null ? null : u.JobTitle.Name, Dept = u.Department == null ? null : u.Department.Name, u.BirthDate })
            .ToListAsync(ct);
        return people.Select(p =>
            {
                var b = p.BirthDate!.Value;
                var next = new DateOnly(today.Year, b.Month, Math.Min(b.Day, DateTime.DaysInMonth(today.Year, b.Month)));
                if (next < today) next = next.AddYears(1);
                return (p, next);
            })
            .Where(x => x.next.DayNumber - today.DayNumber <= days)
            .OrderBy(x => x.next)
            .Select(x => new BirthdayEntry(x.p.Id, x.p.Name, x.p.Title, x.p.Dept, x.next.Month, x.next.Day, x.next == today))
            .ToList();
    }
}
