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

[Route("api/jobs")]
public class JobsController(CollaborationService svc) : ApiController
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] bool all = false, CancellationToken ct = default) => Ok(await svc.JobsAsync(!(all && Me.Has(Permissions.ContentManage)), false, ct));
    [HasPermission(Permissions.ContentManage), HttpPost] public async Task<IActionResult> Create(SaveJobRequest req, CancellationToken ct) => Created(await svc.SaveJobAsync(null, req, ct), "İlan yayınlandı.");
    [HasPermission(Permissions.ContentManage), HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, SaveJobRequest req, CancellationToken ct) => Ok(await svc.SaveJobAsync(id, req, ct), "İlan güncellendi.");
    [HasPermission(Permissions.ContentManage), HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteJobAsync(id, ct); return NoContent(); }
}
