using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

/// <summary>Dosya yükleme ve yetki kontrollü indirme.</summary>
[Route("api/files")]
public class FilesController(FileStorage storage, AppDbContext db, ITicketService tickets) : ApiController
{
    [HttpPost, RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        var stored = await storage.SaveAsync(file, Me.Id, ct);
        await db.SaveChangesAsync(ct);
        return Created(new FileDto(stored.Id, stored.FileName, stored.ContentType, stored.Size, stored.CreatedAt), "Dosya yüklendi.");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await db.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct) ?? throw AppException.NotFound("Dosya");
        if (!await CanReadAsync(file, ct)) throw AppException.Forbidden("Bu dosyaya erişim yetkiniz bulunmamaktadır.");
        await db.Documents.Where(d => d.FileId == id).ExecuteUpdateAsync(s => s.SetProperty(d => d.Downloads, d => d.Downloads + 1), ct);
        return File(storage.Open(file), file.ContentType, file.FileName);
    }

    private async Task<bool> CanReadAsync(StoredFile file, CancellationToken ct)
    {
        var me = Me;
        if (file.UploadedById == me.Id || me.IsAdmin) return true;
        var ticketIds = await db.TicketAttachments.Where(a => a.FileId == file.Id).Select(a => a.TicketId).ToListAsync(ct);
        if (ticketIds.Count > 0 && await db.Tickets.Where(t => ticketIds.Contains(t.Id)).AnyAsync(await tickets.VisibilityAsync(me, ct), ct)) return true;
        var subs = await db.SubmissionFiles.Where(s => s.FileId == file.Id).Select(s => s.Submission).ToListAsync(ct);
        if (subs.Any(s => s.SubmitterId == me.Id || s.AssigneeId == me.Id || s.PendingApproverIds.Contains(me.Id)) || (subs.Count > 0 && me.Has(Permissions.SubmissionsViewAll))) return true;
        if (subs.Count > 0 && await db.SubmissionActions.AnyAsync(a => subs.Select(s => s.Id).Contains(a.SubmissionId) && a.ActorId == me.Id, ct)) return true;
        var doc = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.FileId == file.Id, ct);
        return doc is not null && (doc.DepartmentId is null || doc.DepartmentId == me.DepartmentId || me.Has(Permissions.DocumentsManage));
    }
}
