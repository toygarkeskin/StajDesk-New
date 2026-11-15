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

public class OrganizationService(AppDbContext db, ICacheService cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public Task<List<DepartmentDto>> DepartmentsAsync(CancellationToken ct) =>
        cache.GetOrSetAsync(CacheKeys.Departments, () => db.Departments.AsNoTracking().OrderBy(d => d.Type).ThenBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Code, d.Type, d.ParentId, d.Manager == null ? null : new Ref(d.Manager.Id, d.Manager.FirstName + " " + d.Manager.LastName),
                d.Users.Count(u => u.IsActive), d.IsActive)).ToListAsync(ct), Ttl);

    public async Task<DepartmentDto> SaveDepartmentAsync(int? id, SaveDepartmentRequest req, CancellationToken ct)
    {
        var name = req.Name.Trim();
        if (await db.Departments.AnyAsync(d => d.Name == name && d.Id != id, ct)) throw AppException.Conflict("Bu isimde bir departman zaten var.");
        Department d;
        if (id is { } existing) d = await db.Departments.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw AppException.NotFound("Departman");
        else db.Departments.Add(d = new Department { Name = name });
        if (req.ParentId == id && id is not null) throw AppException.BadRequest("Departman kendi üst birimi olamaz.");
        d.Name = name; d.Code = req.Code?.Trim(); d.Type = req.Type; d.ParentId = req.ParentId; d.ManagerId = req.ManagerId; d.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Departments);
        return (await DepartmentsAsync(ct)).First(x => x.Id == d.Id);
    }

    public Task<List<JobTitleDto>> TitlesAsync(CancellationToken ct) =>
        cache.GetOrSetAsync(CacheKeys.JobTitles, () => db.JobTitles.AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new JobTitleDto(t.Id, t.Name, db.Users.Count(u => u.JobTitleId == t.Id))).ToListAsync(ct), Ttl);

    public async Task<JobTitleDto> AddTitleAsync(SaveJobTitleRequest req, CancellationToken ct)
    {
        var name = req.Name.Trim();
        if (name.Length is < 2 or > 150) throw AppException.BadRequest("Unvan 2-150 karakter olmalıdır.");
        if (await db.JobTitles.AnyAsync(t => t.Name == name, ct)) throw AppException.Conflict("Bu unvan zaten tanımlı.");
        var t = new JobTitle { Name = name };
        db.JobTitles.Add(t);
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.JobTitles);
        return new JobTitleDto(t.Id, t.Name, 0);
    }

    public async Task DeleteTitleAsync(int id, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.JobTitleId == id, ct)) throw AppException.Conflict("Bu unvana sahip çalışanlar olduğu için silinemez.");
        await db.JobTitles.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
        await cache.RemoveAsync(CacheKeys.JobTitles);
    }

    public Task<List<GroupDto>> GroupsAsync(CancellationToken ct) =>
        db.Groups.AsNoTracking().OrderBy(g => g.Name).Select(g => new GroupDto(g.Id, g.Name, g.Description,
            g.Department == null ? null : new DeptRef(g.Department.Id, g.Department.Name), g.IsSupportGroup,
            g.Members.Select(m => new GroupMemberDto(m.UserId, m.User.FirstName + " " + m.User.LastName, m.User.JobTitle == null ? null : m.User.JobTitle.Name, m.IsLead)).ToList(),
            db.Tickets.Count(t => t.GroupId == g.Id && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed))).ToListAsync(ct);

    public async Task<GroupDto> SaveGroupAsync(int? id, SaveGroupRequest req, CancellationToken ct)
    {
        var name = req.Name.Trim();
        if (await db.Groups.AnyAsync(g => g.Name == name && g.Id != id, ct)) throw AppException.Conflict("Bu isimde bir grup zaten var.");
        Group g;
        if (id is { } existing) g = await db.Groups.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw AppException.NotFound("Grup");
        else db.Groups.Add(g = new Group { Name = name });
        g.Name = name; g.Description = req.Description?.Trim(); g.DepartmentId = req.DepartmentId; g.IsSupportGroup = req.IsSupportGroup;
        if (req.MemberIds is { } members)
        {
            var valid = await db.Users.Where(u => members.Contains(u.Id) && u.IsActive).Select(u => u.Id).ToListAsync(ct);
            g.Members.Clear();
            foreach (var uid in valid) g.Members.Add(new GroupMember { UserId = uid, IsLead = req.LeadIds?.Contains(uid) == true });
        }
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Groups);
        return (await GroupsAsync(ct)).First(x => x.Id == g.Id);
    }

    public async Task DeleteGroupAsync(int id, CancellationToken ct)
    {
        if (await db.Tickets.AnyAsync(t => t.GroupId == id && t.Status != TicketStatus.Closed, ct)) throw AppException.Conflict("Gruba atanmış açık talepler olduğu için silinemez.");
        await db.Groups.Where(g => g.Id == id).ExecuteDeleteAsync(ct);
    }

    public Task<List<RoleDto>> RolesAsync(CancellationToken ct) =>
        db.Roles.AsNoTracking().OrderBy(r => r.Id).Select(r => new RoleDto(r.Id, r.Code, r.Name, r.Description, r.IsSystem, r.Permissions, r.Users.Count)).ToListAsync(ct);

    public async Task<RoleDto> SaveRoleAsync(int? id, SaveRoleRequest req, CancellationToken ct)
    {
        Role r;
        if (id is { } existing)
        {
            r = await db.Roles.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw AppException.NotFound("Rol");
            if (r.Code == RoleCodes.SuperAdmin) throw AppException.BadRequest("Super Admin rolünün yetkileri değiştirilemez.");
            if (r.IsSystem && r.Code != req.Code) throw AppException.BadRequest("Sistem rollerinin kodu değiştirilemez.");
        }
        else
        {
            if (await db.Roles.AnyAsync(x => x.Code == req.Code, ct)) throw AppException.Conflict("Bu rol kodu zaten var.");
            db.Roles.Add(r = new Role { Code = req.Code, Name = req.Name });
        }
        r.Code = req.Code; r.Name = req.Name.Trim(); r.Description = req.Description?.Trim(); r.Permissions = req.Permissions.Distinct().ToList();
        await db.SaveChangesAsync(ct);
        return (await RolesAsync(ct)).First(x => x.Id == r.Id);
    }

    public async Task DeleteRoleAsync(int id, CancellationToken ct)
    {
        var r = await db.Roles.Include(x => x.Users).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Rol");
        if (r.IsSystem) throw AppException.BadRequest("Sistem rolleri silinemez.");
        if (r.Users.Count > 0) throw AppException.Conflict("Bu role atanmış kullanıcılar var.");
        db.Roles.Remove(r);
        await db.SaveChangesAsync(ct);
    }
}
