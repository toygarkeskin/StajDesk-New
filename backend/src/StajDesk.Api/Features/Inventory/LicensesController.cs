using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

[Route("api/licenses")]
[HasPermission(Permissions.InventoryView)]
public class LicensesController(InventoryService inventory) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await inventory.LicensesAsync(ct));

    [HasPermission(Permissions.InventoryManage), HttpPost]
    public async Task<IActionResult> Create(SaveLicenseRequest req, CancellationToken ct) => Created(await inventory.SaveLicenseAsync(null, req, ct), "Lisans eklendi.");

    [HasPermission(Permissions.InventoryManage), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveLicenseRequest req, CancellationToken ct) => Ok(await inventory.SaveLicenseAsync(id, req, ct), "Lisans güncellendi.");

    [HasPermission(Permissions.InventoryManage), HttpPost("{id:int}/install")]
    public async Task<IActionResult> Install(int id, InstallRequest req, CancellationToken ct) { await inventory.InstallAsync(id, req.AssetId, ct); return NoContent(); }

    [HasPermission(Permissions.InventoryManage), HttpDelete("installations/{installationId:long}")]
    public async Task<IActionResult> Uninstall(long installationId, CancellationToken ct) { await inventory.UninstallAsync(installationId, ct); return NoContent(); }
}
