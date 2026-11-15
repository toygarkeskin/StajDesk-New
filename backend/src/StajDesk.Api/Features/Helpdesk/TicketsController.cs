using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

[Route("api/tickets")]
public class TicketsController(ITicketService tickets) : ApiController
{
    /// <summary>Talep listesi. Sonuçlar kullanıcının yetki kapsamına göre sunucu tarafında filtrelenir.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TicketQuery q, CancellationToken ct) => Ok(await tickets.ListAsync(q, Me, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await tickets.GetAsync(id, Me, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateTicketRequest req, CancellationToken ct)
    {
        var t = await tickets.CreateAsync(req, Me, ct);
        return Created(t, $"#{t.Id} numaralı talebiniz oluşturuldu.");
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateTicketRequest req, CancellationToken ct) => Ok(await tickets.UpdateAsync(id, req, Me, ct), "Talep güncellendi.");

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, ChangeStatusRequest req, CancellationToken ct) => Ok(await tickets.ChangeStatusAsync(id, req, Me, ct), "Talep durumu güncellendi.");

    [HasPermission(Permissions.TicketsManage), HttpPatch("{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, AssignTicketRequest req, CancellationToken ct) => Ok(await tickets.AssignAsync(id, req, Me, ct), "Atama güncellendi.");

    [HttpPost("{id:int}/comments")]
    public async Task<IActionResult> Comment(int id, AddCommentRequest req, CancellationToken ct) => Created(await tickets.AddCommentAsync(id, req, Me, ct), "Yanıtınız eklendi.");

    /// <summary>Talebe dosya ekler (önce /api/files ile yüklenen dosya kimlikleri).</summary>
    [HttpPost("{id:int}/attachments")]
    public async Task<IActionResult> Attach(int id, [FromBody] List<Guid> fileIds, [FromServices] AppDbContext db, CancellationToken ct)
    {
        var owned = await db.Files.Where(f => fileIds.Contains(f.Id) && f.UploadedById == Me.Id).Select(f => f.Id).ToListAsync(ct);
        return Ok(await tickets.AttachAsync(id, owned, Me, ct), "Dosya eklendi.");
    }

    [HasPermission(Permissions.TicketsDelete), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await tickets.DeleteAsync(id, Me, ct); return NoContent(); }
}
