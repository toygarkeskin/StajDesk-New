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

[Route("api/banners")]
[HasPermission(Permissions.ContentManage)]
public class BannersController(CollaborationService svc) : ApiController
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await svc.BannersAsync(ct));
    [HttpPost] public async Task<IActionResult> Create(SaveBannerRequest req, CancellationToken ct) => Created(await svc.SaveBannerAsync(null, req, ct), "Banner eklendi.");
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, SaveBannerRequest req, CancellationToken ct) => Ok(await svc.SaveBannerAsync(id, req, ct), "Banner güncellendi.");
    [HttpPut("order")] public async Task<IActionResult> Reorder(ReorderRequest req, CancellationToken ct) { await svc.ReorderBannersAsync(req, ct); return NoContent(); }
    [HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteBannerAsync(id, ct); return NoContent(); }
}
