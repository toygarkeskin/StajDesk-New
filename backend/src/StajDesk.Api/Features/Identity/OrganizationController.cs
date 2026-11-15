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

[Route("api/org")]
public class OrganizationController(OrganizationService org) : ApiController
{
    [HttpGet("departments")]
    public async Task<IActionResult> Departments(CancellationToken ct) => Ok(await org.DepartmentsAsync(ct));

    [HasPermission(Permissions.OrgManage), HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment(SaveDepartmentRequest req, CancellationToken ct) => Created(await org.SaveDepartmentAsync(null, req, ct), "Departman eklendi.");

    [HasPermission(Permissions.OrgManage), HttpPut("departments/{id:int}")]
    public async Task<IActionResult> UpdateDepartment(int id, SaveDepartmentRequest req, CancellationToken ct) => Ok(await org.SaveDepartmentAsync(id, req, ct), "Departman güncellendi.");

    [HttpGet("titles")]
    public async Task<IActionResult> Titles(CancellationToken ct) => Ok(await org.TitlesAsync(ct));

    [HasPermission(Permissions.OrgManage), HttpPost("titles")]
    public async Task<IActionResult> AddTitle(SaveJobTitleRequest req, CancellationToken ct) => Created(await org.AddTitleAsync(req, ct), "Unvan eklendi.");

    [HasPermission(Permissions.OrgManage), HttpDelete("titles/{id:int}")]
    public async Task<IActionResult> DeleteTitle(int id, CancellationToken ct) { await org.DeleteTitleAsync(id, ct); return NoContent(); }

    [HttpGet("groups")]
    public async Task<IActionResult> Groups(CancellationToken ct) => Ok(await org.GroupsAsync(ct));

    [HasPermission(Permissions.OrgManage, Permissions.TicketsConfigure), HttpPost("groups")]
    public async Task<IActionResult> CreateGroup(SaveGroupRequest req, CancellationToken ct) => Created(await org.SaveGroupAsync(null, req, ct), "Grup oluşturuldu.");

    [HasPermission(Permissions.OrgManage, Permissions.TicketsConfigure), HttpPut("groups/{id:int}")]
    public async Task<IActionResult> UpdateGroup(int id, SaveGroupRequest req, CancellationToken ct) => Ok(await org.SaveGroupAsync(id, req, ct), "Grup güncellendi.");

    [HasPermission(Permissions.OrgManage, Permissions.TicketsConfigure), HttpDelete("groups/{id:int}")]
    public async Task<IActionResult> DeleteGroup(int id, CancellationToken ct) { await org.DeleteGroupAsync(id, ct); return NoContent(); }

    [HasPermission(Permissions.UsersManage, Permissions.RolesManage), HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken ct) => Ok(await org.RolesAsync(ct));

    [HasPermission(Permissions.UsersManage, Permissions.RolesManage), HttpGet("permissions")]
    public IActionResult PermissionCatalog() => Ok(Permissions.All.Select(p => new PermissionDto(p.Code, p.Group, p.Label)).ToList());

    [HasPermission(Permissions.RolesManage), HttpPost("roles")]
    public async Task<IActionResult> CreateRole(SaveRoleRequest req, CancellationToken ct) => Created(await org.SaveRoleAsync(null, req, ct), "Rol oluşturuldu.");

    [HasPermission(Permissions.RolesManage), HttpPut("roles/{id:int}")]
    public async Task<IActionResult> UpdateRole(int id, SaveRoleRequest req, CancellationToken ct) => Ok(await org.SaveRoleAsync(id, req, ct), "Rol güncellendi.");

    [HasPermission(Permissions.RolesManage), HttpDelete("roles/{id:int}")]
    public async Task<IActionResult> DeleteRole(int id, CancellationToken ct) { await org.DeleteRoleAsync(id, ct); return NoContent(); }
}
