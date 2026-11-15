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

public class AccountRequestService(AppDbContext db, UserService users, IEventBus events, TimeProvider clock, ILogger<AccountRequestService> logger)
{
    public Task<List<AccountRequestDto>> ListAsync(UserContext me, AccountRequestStatus? status, CancellationToken ct)
    {
        var q = db.AccountRequests.AsNoTracking().AsQueryable();
        if (!me.Has(Permissions.AccountsApprove)) q = q.Where(r => r.RequestedById == me.Id);
        if (status is { } s) q = q.Where(r => r.Status == s);
        return q.OrderByDescending(r => r.CreatedAt).Select(r => new AccountRequestDto(r.Id, r.FirstName, r.LastName, r.Email, r.EmployeeNo,
            new DeptRef(r.Department.Id, r.Department.Name), r.JobTitle == null ? null : r.JobTitle.Name,
            r.Manager == null ? null : new Ref(r.Manager.Id, r.Manager.FirstName + " " + r.Manager.LastName), r.StartDate, r.EmploymentType, r.Phone, r.Note, r.Status,
            new Ref(r.RequestedBy.Id, r.RequestedBy.FirstName + " " + r.RequestedBy.LastName), r.CreatedAt,
            r.ProcessedBy == null ? null : new Ref(r.ProcessedBy.Id, r.ProcessedBy.FirstName + " " + r.ProcessedBy.LastName), r.ProcessedAt, r.RejectReason, r.CreatedUserId))
            .Take(200).ToListAsync(ct);
    }

    public async Task<int> CreateAsync(CreateAccountRequest req, UserContext me, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) throw AppException.Conflict("Bu e-posta adresiyle kayıtlı bir hesap zaten var.");
        if (await db.AccountRequests.AnyAsync(r => r.Email == email && r.Status == AccountRequestStatus.Pending, ct)) throw AppException.Conflict("Bu e-posta için bekleyen bir talep var.");
        var r = new AccountRequest
        {
            RequestedById = me.Id, FirstName = req.FirstName.Trim(), LastName = req.LastName.Trim(), Email = email, EmployeeNo = req.EmployeeNo?.Trim(),
            DepartmentId = req.DepartmentId, JobTitleId = req.JobTitleId, ManagerId = req.ManagerId, StartDate = req.StartDate, EmploymentType = req.EmploymentType,
            Phone = req.Phone?.Trim(), Note = req.Note?.Trim(), Status = AccountRequestStatus.Pending,
        };
        db.AccountRequests.Add(r);
        var approvers = await db.Users.Where(u => u.IsActive && u.Roles.Any(x => x.Role.Permissions.Contains(Permissions.AccountsApprove))).Select(u => u.Id).ToListAsync(ct);
        events.Enqueue(new AccountEvent("request_created", $"{r.FirstName} {r.LastName}", null, approvers) { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Hesap talebi oluşturuldu {RequestId}", r.Id);
        return r.Id;
    }

    public async Task<CreatedUserResponse> ApproveAsync(int id, UserContext me, CancellationToken ct)
    {
        var r = await db.AccountRequests.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Hesap talebi");
        if (r.Status != AccountRequestStatus.Pending) throw AppException.BadRequest("Talep daha önce işlenmiş.");
        var temp = PasswordPolicy.Generate();
        var user = await users.CreateFromRequestAsync(r, temp, me, ct);
        r.Status = AccountRequestStatus.Approved;
        r.ProcessedById = me.Id;
        r.ProcessedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        r.CreatedUserId = user.Id;
        events.Enqueue(new AccountEvent("account_created", user.FullName, user.Id, [r.RequestedById]) { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Hesap talebi onaylandı {RequestId} → kullanıcı {UserId}", id, user.Id);
        return new CreatedUserResponse(await users.GetAsync(user.Id, ct), temp);
    }

    public async Task RejectAsync(int id, RejectRequest req, UserContext me, CancellationToken ct)
    {
        var r = await db.AccountRequests.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Hesap talebi");
        if (r.Status != AccountRequestStatus.Pending) throw AppException.BadRequest("Talep daha önce işlenmiş.");
        if (string.IsNullOrWhiteSpace(req.Reason)) throw AppException.BadRequest("Ret gerekçesi zorunludur.");
        r.Status = AccountRequestStatus.Rejected;
        r.RejectReason = req.Reason.Trim();
        r.ProcessedById = me.Id;
        r.ProcessedAt = clock.GetUtcNow().UtcDateTime;
        events.Enqueue(new DirectNotificationEvent([r.RequestedById], "Hesap talebi reddedildi", $"{r.FirstName} {r.LastName} için hesap talebi reddedildi: {r.RejectReason}", "warning", "/admin/account-requests") { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
    }
}
