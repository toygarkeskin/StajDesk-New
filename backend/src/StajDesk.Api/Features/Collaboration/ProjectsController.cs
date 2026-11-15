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

[Route("api/projects")]
public class ProjectsController(CollaborationService svc) : ApiController
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await svc.ProjectsAsync(ct));
    [HasPermission(Permissions.ProjectsManage), HttpPost] public async Task<IActionResult> Create(SaveProjectRequest req, CancellationToken ct) => Created(await svc.SaveProjectAsync(null, req, ct), "Proje oluşturuldu.");
    [HasPermission(Permissions.ProjectsManage), HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, SaveProjectRequest req, CancellationToken ct) => Ok(await svc.SaveProjectAsync(id, req, ct), "Proje güncellendi.");
}
