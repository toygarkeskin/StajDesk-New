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

[Route("api/notifications")]
public class NotificationsController(CollaborationService svc) : ApiController
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] bool unreadOnly = false, [FromQuery] int take = 30, CancellationToken ct = default) => Ok(await svc.NotificationsAsync(Me.Id, unreadOnly, take, ct));
    [HttpGet("unread-count")] public async Task<IActionResult> Unread(CancellationToken ct) => Ok(await svc.UnreadNotificationsAsync(Me.Id, ct));
    [HttpPost("{id:long}/read")] public async Task<IActionResult> Read(long id, CancellationToken ct) { await svc.MarkReadAsync(Me.Id, id, ct); return NoContent(); }
    [HttpPost("read-all")] public async Task<IActionResult> ReadAll(CancellationToken ct) { await svc.MarkReadAsync(Me.Id, null, ct); return NoContent(); }
}
