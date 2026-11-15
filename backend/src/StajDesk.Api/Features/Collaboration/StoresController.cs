using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;
using StajDesk.Infrastructure.Seed;

namespace StajDesk.Api.Features;

[Route("api/stores")]
public class StoresController(CollaborationService svc) : ApiController
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] PagedQuery q, [FromQuery] string? city, [FromQuery] StoreStatus? status, CancellationToken ct) => Ok(await svc.StoresAsync(q, city, status, ct));
    [HttpGet("cities")] public async Task<IActionResult> Cities(CancellationToken ct) => Ok(await svc.StoreCitiesAsync(ct));
    [HasPermission(Permissions.StoresManage), HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, SaveStoreRequest req, CancellationToken ct) => Ok(await svc.SaveStoreAsync(id, req, ct), "Mağaza güncellendi.");
}
