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

[Route("api/events")]
public class EventsController(CollaborationService svc) : ApiController
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] bool upcoming = true, CancellationToken ct = default) => Ok(await svc.EventsAsync(Me, upcoming, ct));
    [HasPermission(Permissions.ContentManage), HttpPost] public async Task<IActionResult> Create(SaveEventRequest req, CancellationToken ct) => Created(await svc.SaveEventAsync(null, req, Me, ct), "Etkinlik eklendi.");
    [HasPermission(Permissions.ContentManage), HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, SaveEventRequest req, CancellationToken ct) => Ok(await svc.SaveEventAsync(id, req, Me, ct), "Etkinlik güncellendi.");
    [HttpPost("{id:int}/attend")] public async Task<IActionResult> Attend(int id, CancellationToken ct) => Ok(new { attending = await svc.ToggleAttendAsync(id, Me, ct) });
    [HasPermission(Permissions.ContentManage), HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteEventAsync(id, ct); return NoContent(); }
}
