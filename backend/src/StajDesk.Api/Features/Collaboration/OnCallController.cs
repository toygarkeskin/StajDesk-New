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

[Route("api/on-call")]
public class OnCallController(CollaborationService svc) : ApiController
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] bool current = false, CancellationToken ct = default) => Ok(await svc.OnCallAsync(current, ct));
    [HasPermission(Permissions.OnCallManage), HttpPost] public async Task<IActionResult> Create(SaveOnCallRequest req, CancellationToken ct) => Created(await svc.AddOnCallAsync(req, ct), "Nöbet eklendi.");
    [HasPermission(Permissions.OnCallManage), HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteOnCallAsync(id, ct); return NoContent(); }
}
