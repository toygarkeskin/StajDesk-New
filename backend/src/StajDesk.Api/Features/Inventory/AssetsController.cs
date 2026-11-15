using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

[Route("api/assets")]
public class AssetsController(InventoryService inventory) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AssetQuery q, CancellationToken ct) => Ok(await inventory.ListAsync(q, Me, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await inventory.GetAsync(id, Me, ct));

    [HasPermission(Permissions.InventoryManage), HttpPost]
    public async Task<IActionResult> Create(SaveAssetRequest req, CancellationToken ct) => Created(await inventory.SaveAsync(null, req, ct), "Varlık eklendi.");

    [HasPermission(Permissions.InventoryManage), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveAssetRequest req, CancellationToken ct) => Ok(await inventory.SaveAsync(id, req, ct), "Varlık güncellendi.");

    [HasPermission(Permissions.InventoryManage), HttpPost("{id:int}/checkout")]
    public async Task<IActionResult> Checkout(int id, CheckoutRequest req, CancellationToken ct) => Ok(await inventory.CheckoutAsync(id, req, Me, ct), "Zimmet kaydı oluşturuldu.");

    [HasPermission(Permissions.InventoryManage), HttpPost("{id:int}/checkin")]
    public async Task<IActionResult> Checkin(int id, CheckinRequest req, CancellationToken ct) => Ok(await inventory.CheckinAsync(id, req, Me, ct), "İade alındı.");

    [HasPermission(Permissions.InventoryManage), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await inventory.DeleteAsync(id, ct); return NoContent(); }
}
