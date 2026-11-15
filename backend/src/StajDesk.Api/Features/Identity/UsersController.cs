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

[Route("api/users")]
public class UsersController(UserService users) : ApiController
{
    [HasPermission(Permissions.UsersManage), HttpGet]
    public async Task<IActionResult> List([FromQuery] UserQuery q, CancellationToken ct) => Ok(await users.ListAsync(q, ct));

    [HasPermission(Permissions.UsersManage), HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await users.GetAsync(id, ct));

    /// <summary>Yeni hesap oluşturur; geçici parola yalnızca bu yanıtta bir kez gösterilir.</summary>
    [HasPermission(Permissions.UsersManage), HttpPost]
    public async Task<IActionResult> Create(SaveUserRequest req, CancellationToken ct) => Created(await users.CreateAsync(req, Me, ct), "Hesap oluşturuldu.");

    [HasPermission(Permissions.UsersManage), HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveUserRequest req, CancellationToken ct) => Ok(await users.UpdateAsync(id, req, ct), "Kullanıcı güncellendi.");

    [HasPermission(Permissions.UsersManage, Permissions.RolesManage), HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, SetRolesRequest req, CancellationToken ct) => Ok(await users.SetRolesAsync(id, req, Me, ct), "Roller güncellendi.");

    [HasPermission(Permissions.UsersManage), HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, DeactivateRequest req, CancellationToken ct) => Ok(await users.DeactivateAsync(id, req, Me, ct), "Hesap devre dışı bırakıldı.");

    [HasPermission(Permissions.UsersManage), HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => Ok(await users.ActivateAsync(id, ct), "Hesap etkinleştirildi.");

    [HasPermission(Permissions.UsersManage), HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken ct) => Ok(new { temporaryPassword = await users.ResetPasswordAsync(id, Me, ct) }, "Geçici parola oluşturuldu.");
}
