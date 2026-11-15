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

[Route("api/messages")]
public class MessagesController(CollaborationService svc) : ApiController
{
    [HttpGet("conversations")] public async Task<IActionResult> Conversations(CancellationToken ct) => Ok(await svc.ConversationsAsync(Me, ct));
    [HttpGet("{userId:guid}")] public async Task<IActionResult> Thread(Guid userId, CancellationToken ct) => Ok(await svc.ThreadAsync(userId, Me, ct));
    [HttpPost] public async Task<IActionResult> Send(SendMessageRequest req, CancellationToken ct) => Created(await svc.SendAsync(req, Me, ct));
    [HttpGet("unread-count")] public async Task<IActionResult> Unread(CancellationToken ct) => Ok(await svc.UnreadMessagesAsync(Me.Id, ct));
}
