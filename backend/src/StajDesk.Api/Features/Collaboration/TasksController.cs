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

[Route("api/tasks")]
public class TasksController(CollaborationService svc) : ApiController
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] TaskQuery q, CancellationToken ct) => Ok(await svc.TasksAsync(q, Me, ct));
    [HttpPost] public async Task<IActionResult> Create(SaveTaskRequest req, CancellationToken ct) => Created(await svc.SaveTaskAsync(null, req, Me, ct), "Görev oluşturuldu.");
    [HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, SaveTaskRequest req, CancellationToken ct) => Ok(await svc.SaveTaskAsync(id, req, Me, ct), "Görev güncellendi.");
    [HttpPatch("{id:int}/status")] public async Task<IActionResult> Move(int id, MoveTaskRequest req, CancellationToken ct) { await svc.MoveTaskAsync(id, req, Me, ct); return NoContent(); }
    [HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteTaskAsync(id, Me, ct); return NoContent(); }
}
