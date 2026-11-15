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

[Route("api/achievements")]
public class AchievementsController(CollaborationService svc) : ApiController
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] int take = 30, CancellationToken ct = default) => Ok(await svc.AchievementsAsync(take, ct));
    [HttpPost] public async Task<IActionResult> Give(GiveKudosRequest req, CancellationToken ct) => Created(await svc.GiveAsync(req, Me, ct), "Takdiriniz iletildi.");
    [HttpPost("{id:int}/like")] public async Task<IActionResult> Like(int id, CancellationToken ct) => Ok(new { likes = await svc.LikeAsync(id, ct) });
}
