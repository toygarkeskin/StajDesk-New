using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

[Route("api/ticket-categories")]
public class TicketCategoriesController(HelpdeskConfigService config) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await config.CategoriesAsync(ct));

    [HasPermission(Permissions.TicketsConfigure), HttpPost]
    public async Task<IActionResult> Create(SaveCategoryRequest req, CancellationToken ct) => Created(await config.SaveCategoryAsync(null, req, ct), "Kategori eklendi.");

    [HasPermission(Permissions.TicketsConfigure), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveCategoryRequest req, CancellationToken ct) => Ok(await config.SaveCategoryAsync(id, req, ct), "Kategori güncellendi.");

    [HasPermission(Permissions.TicketsConfigure), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await config.DeleteCategoryAsync(id, ct); return NoContent(); }
}
