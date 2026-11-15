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

public class UserService(AppDbContext db, IPasswordHasher<User> hasher, IEventBus events, ICacheService cache, TimeProvider clock, ILogger<UserService> logger)
{
    private static readonly string[] PrivilegedRoles = [RoleCodes.SuperAdmin, RoleCodes.Admin];

    public Task<PagedResult<UserListItem>> ListAsync(UserQuery q, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (q.DepartmentId is { } d) query = query.Where(u => u.DepartmentId == d);
        if (q.IsActive is { } a) query = query.Where(u => u.IsActive == a);
        if (!string.IsNullOrWhiteSpace(q.Role)) query = query.Where(u => u.Roles.Any(r => r.Role.Code == q.Role));
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var t = q.Search.Like();
            query = query.Where(u => EF.Functions.ILike(u.FirstName + " " + u.LastName, t) || EF.Functions.ILike(u.Email, t) || (u.EmployeeNo != null && EF.Functions.ILike(u.EmployeeNo, t)));
        }
        return query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToPagedAsync(q, u => new UserListItem(u.Id, u.FirstName + " " + u.LastName, u.Email, u.EmployeeNo,
            u.Department == null ? null : new DeptRef(u.Department.Id, u.Department.Name), u.JobTitle == null ? null : u.JobTitle.Name,
            u.Roles.Select(r => r.Role.Code).ToList(), u.IsActive, u.MustChangePassword, u.LastLoginAt, u.CreatedAt), ct);
    }

    public async Task<UserDetail> GetAsync(Guid id, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.Department).Include(x => x.JobTitle).Include(x => x.Manager)
            .Include(x => x.Roles).ThenInclude(r => r.Role).Include(x => x.Groups).ThenInclude(g => g.Group)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Kullanıcı");
        var assets = await db.Assets.CountAsync(a => a.AssignedToId == id, ct);
        var tickets = await db.Tickets.CountAsync(t => t.RequesterId == id && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved, ct);
        return new UserDetail(u.Id, u.FirstName, u.LastName, u.FullName, u.Email, u.EmployeeNo, u.DepartmentId, u.Department?.Name, u.JobTitleId, u.JobTitle?.Name,
            u.Manager is null ? null : new Ref(u.Manager.Id, u.Manager.FullName), u.Phone, u.BirthDate, u.HireDate, u.EmploymentType, u.Location,
            u.IsActive, u.DeactivatedAt, u.DeactivationReason, u.MustChangePassword, u.LastLoginAt, u.CreatedAt,
            u.Roles.Select(r => new RoleRef(r.Role.Code, r.Role.Name)).ToList(), u.Groups.Select(g => g.Group.Name).ToList(), assets, tickets);
    }

    public async Task<CreatedUserResponse> CreateAsync(SaveUserRequest req, UserContext actor, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) throw AppException.Conflict("Bu e-posta adresiyle kayıtlı bir hesap bulunmaktadır.");
        if (req.EmployeeNo is { } no && await db.Users.AnyAsync(u => u.EmployeeNo == no, ct)) throw AppException.Conflict("Bu personel numarası kullanılmaktadır.");

        var user = new User { FirstName = req.FirstName.Trim(), LastName = req.LastName.Trim(), Email = email, MustChangePassword = true };
        await ApplyAsync(user, req, ct);
        var temp = PasswordPolicy.Generate();
        user.PasswordHash = hasher.HashPassword(user, temp);
        await SetRolesInternalAsync(user, req.RoleCodes is { Count: > 0 } r ? r : [RoleCodes.Employee], actor, ct);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Hesap oluşturuldu {UserId} (işlemi yapan {ActorId})", user.Id, actor.Id);
        return new CreatedUserResponse(await GetAsync(user.Id, ct), temp);
    }

    public async Task<UserDetail> UpdateAsync(Guid id, SaveUserRequest req, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound("Kullanıcı");
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != id, ct)) throw AppException.Conflict("Bu e-posta adresi başka bir hesapta kullanılıyor.");
        if (req.ManagerId == id) throw AppException.BadRequest("Kullanıcı kendi yöneticisi olamaz.");
        user.FirstName = req.FirstName.Trim();
        user.LastName = req.LastName.Trim();
        user.Email = email;
        await ApplyAsync(user, req, ct);
        await db.SaveChangesAsync(ct);
        await cache.RemoveByPrefixAsync(CacheKeys.StatsPrefix);
        return await GetAsync(id, ct);
    }

    public async Task<UserDetail> SetRolesAsync(Guid id, SetRolesRequest req, UserContext actor, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Roles).ThenInclude(r => r.Role).FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound("Kullanıcı");
        if (id == actor.Id && user.Roles.Any(r => PrivilegedRoles.Contains(r.Role.Code)) && !req.RoleCodes.Any(PrivilegedRoles.Contains))
            throw AppException.BadRequest("Kendi yönetici rolünüzü kaldıramazsınız.");
        await SetRolesInternalAsync(user, req.RoleCodes, actor, ct);
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Rol değişikliği {UserId}: {Roles} (işlemi yapan {ActorId})", id, string.Join(",", req.RoleCodes), actor.Id);
        return await GetAsync(id, ct);
    }

    public async Task<UserDetail> DeactivateAsync(Guid id, DeactivateRequest req, UserContext actor, CancellationToken ct)
    {
        if (id == actor.Id) throw AppException.BadRequest("Kendi hesabınızı devre dışı bırakamazsınız.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound("Kullanıcı");
        if (!user.IsActive) throw AppException.BadRequest("Hesap zaten devre dışı.");
        user.IsActive = false;
        user.DeactivatedAt = clock.GetUtcNow().UtcDateTime;
        user.DeactivationReason = string.IsNullOrWhiteSpace(req.Reason) ? "İşten ayrılış" : req.Reason.Trim();
        await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
        var itStaff = await db.Users.Where(u => u.IsActive && u.Roles.Any(r => r.Role.Code == RoleCodes.It)).Select(u => u.Id).ToListAsync(ct);
        events.Enqueue(new AccountEvent("account_deactivated", user.FullName, user.Id, itStaff) { ActorId = actor.Id, ActorName = actor.Name });
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Hesap devre dışı bırakıldı {UserId} (işlemi yapan {ActorId})", id, actor.Id);
        return await GetAsync(id, ct);
    }

    public async Task<UserDetail> ActivateAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound("Kullanıcı");
        user.IsActive = true;
        user.DeactivatedAt = null;
        user.DeactivationReason = null;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<string> ResetPasswordAsync(Guid id, UserContext actor, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound("Kullanıcı");
        var temp = PasswordPolicy.Generate();
        user.PasswordHash = hasher.HashPassword(user, temp);
        user.MustChangePassword = true;
        await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
        db.AuditLogs.Add(new AuditLog { UserId = actor.Id, UserName = actor.Name, Action = "PasswordReset", EntityType = nameof(User), EntityId = id.ToString(), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Parola sıfırlandı {UserId} (işlemi yapan {ActorId})", id, actor.Id);
        return temp;
    }

    public async Task<User> CreateFromRequestAsync(AccountRequest r, string tempPassword, UserContext actor, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Email == r.Email, ct)) throw AppException.Conflict("Bu e-posta adresiyle kayıtlı bir hesap zaten var.");
        var user = new User
        {
            FirstName = r.FirstName, LastName = r.LastName, Email = r.Email, EmployeeNo = r.EmployeeNo, DepartmentId = r.DepartmentId, JobTitleId = r.JobTitleId,
            ManagerId = r.ManagerId, HireDate = r.StartDate, EmploymentType = r.EmploymentType, Phone = r.Phone, MustChangePassword = true,
        };
        user.PasswordHash = hasher.HashPassword(user, tempPassword);
        await SetRolesInternalAsync(user, [RoleCodes.Employee], actor, ct);
        db.Users.Add(user);
        var entitlement = LeaveRules.AnnualEntitlement(r.StartDate, DateOnly.FromDateTime(DateTime.UtcNow));
        db.LeaveBalances.Add(new LeaveBalance { User = user, Year = DateTime.UtcNow.Year, Entitled = entitlement });
        return user;
    }

    private async Task ApplyAsync(User user, SaveUserRequest req, CancellationToken ct)
    {
        if (req.DepartmentId is { } d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) throw AppException.BadRequest("Departman bulunamadı.");
        if (req.JobTitleId is { } t && !await db.JobTitles.AnyAsync(x => x.Id == t, ct)) throw AppException.BadRequest("Unvan bulunamadı.");
        if (req.ManagerId is { } m && !await db.Users.AnyAsync(x => x.Id == m && x.IsActive, ct)) throw AppException.BadRequest("Yönetici bulunamadı.");
        user.EmployeeNo = string.IsNullOrWhiteSpace(req.EmployeeNo) ? user.EmployeeNo : req.EmployeeNo.Trim();
        user.DepartmentId = req.DepartmentId;
        user.JobTitleId = req.JobTitleId;
        user.ManagerId = req.ManagerId;
        user.Phone = req.Phone?.Trim();
        user.BirthDate = req.BirthDate;
        user.HireDate = req.HireDate;
        user.EmploymentType = req.EmploymentType;
        user.Location = req.Location?.Trim();
    }

    private async Task SetRolesInternalAsync(User user, List<string> codes, UserContext actor, CancellationToken ct)
    {
        codes = codes.Append(RoleCodes.Employee).Distinct().ToList();
        if (codes.Any(PrivilegedRoles.Contains) && !actor.HasRole(RoleCodes.SuperAdmin) && !actor.Has(Permissions.RolesManage))
            throw AppException.Forbidden("Admin ve Super Admin rollerini yalnızca Super Admin atayabilir.");
        var roles = await db.Roles.Where(r => codes.Contains(r.Code)).ToListAsync(ct);
        if (roles.Count != codes.Count) throw AppException.BadRequest("Tanımsız rol kodu.");
        user.Roles.Clear();
        foreach (var r in roles) user.Roles.Add(new UserRole { Role = r, User = user });
    }
}
