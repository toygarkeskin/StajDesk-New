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

[Route("api/announcements")]
public class AnnouncementsController(CollaborationService svc) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AnnouncementKind? kind, [FromQuery] bool manage = false, [FromQuery] int take = 30, CancellationToken ct = default) =>
        Ok(await svc.AnnouncementsAsync(Me, kind, manage && Me.Has(Permissions.ContentManage), take, ct));

    [HasPermission(Permissions.ContentManage), HttpPost]
    public async Task<IActionResult> Create(SaveAnnouncementRequest req, CancellationToken ct) => Created(await svc.SaveAnnouncementAsync(null, req, Me, ct), "Yayınlandı.");

    [HasPermission(Permissions.ContentManage), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveAnnouncementRequest req, CancellationToken ct) => Ok(await svc.SaveAnnouncementAsync(id, req, Me, ct), "Güncellendi.");

    [HasPermission(Permissions.ContentManage), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteAnnouncementAsync(id, ct); return NoContent(); }
}
