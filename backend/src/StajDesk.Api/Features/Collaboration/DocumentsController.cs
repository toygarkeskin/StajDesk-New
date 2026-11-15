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

[Route("api/documents")]
public class DocumentsController(CollaborationService svc) : ApiController
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] string? category, [FromQuery] string? search, CancellationToken ct) => Ok(await svc.DocumentsAsync(Me, category, search, ct));
    [HasPermission(Permissions.DocumentsManage), HttpPost] public async Task<IActionResult> Create(SaveDocumentRequest req, CancellationToken ct) => Created(await svc.AddDocumentAsync(req, Me, ct), "Doküman eklendi.");
    [HasPermission(Permissions.DocumentsManage), HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await svc.DeleteDocumentAsync(id, ct); return NoContent(); }
}
