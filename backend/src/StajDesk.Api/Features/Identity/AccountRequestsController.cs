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

[Route("api/account-requests")]
public class AccountRequestsController(AccountRequestService service) : ApiController
{
    [HasPermission(Permissions.AccountsRequest, Permissions.AccountsApprove), HttpGet]
    public async Task<IActionResult> List([FromQuery] AccountRequestStatus? status, CancellationToken ct) => Ok(await service.ListAsync(Me, status, ct));

    /// <summary>İK, yeni personel için hesap açma talebi gönderir.</summary>
    [HasPermission(Permissions.AccountsRequest), HttpPost]
    public async Task<IActionResult> Create(CreateAccountRequest req, CancellationToken ct) => Created(new { id = await service.CreateAsync(req, Me, ct) }, "Hesap talebi IT ekibine iletildi.");

    /// <summary>IT/Admin talebi onaylar ve hesabı oluşturur.</summary>
    [HasPermission(Permissions.AccountsApprove), HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken ct) => Ok(await service.ApproveAsync(id, Me, ct), "Hesap oluşturuldu.");

    [HasPermission(Permissions.AccountsApprove), HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, RejectRequest req, CancellationToken ct) { await service.RejectAsync(id, req, Me, ct); return NoContent(); }
}
