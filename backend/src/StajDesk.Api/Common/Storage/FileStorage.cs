using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Common;

/// <summary>Yüklenen dosyaları uzantı, MIME türü, imza (magic number) ve boyuta göre doğrular; diske rastgele adla yazar.</summary>
public class FileStorage(IOptions<FileStorageOptions> options, AppDbContext db, ILogger<FileStorage> logger)
{
    private static readonly Dictionary<string, (string Mime, byte[][] Signatures)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", [[0x25, 0x50, 0x44, 0x46]]),
        [".png"] = ("image/png", [[0x89, 0x50, 0x4E, 0x47]]),
        [".jpg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".jpeg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".webp"] = ("image/webp", [[0x52, 0x49, 0x46, 0x46]]),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [[0x50, 0x4B, 0x03, 0x04]]),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [[0x50, 0x4B, 0x03, 0x04]]),
        [".csv"] = ("text/csv", []),
        [".txt"] = ("text/plain", []),
    };

    public string Root => Path.GetFullPath(options.Value.Root);

    public async Task<StoredFile> SaveAsync(IFormFile file, Guid uploaderId, CancellationToken ct)
    {
        if (file.Length == 0) throw AppException.BadRequest("Boş dosya yüklenemez.");
        if (file.Length > options.Value.MaxBytes)
            throw AppException.BadRequest($"Dosya boyutu en fazla {options.Value.MaxBytes / 1024 / 1024} MB olabilir.");
        var ext = Path.GetExtension(file.FileName);
        if (!Allowed.TryGetValue(ext, out var rule))
            throw AppException.BadRequest("Bu dosya türü desteklenmiyor. İzin verilen türler: PDF, PNG, JPG, WEBP, DOCX, XLSX, CSV, TXT.");

        await using var input = file.OpenReadStream();
        var head = new byte[8];
        var read = await input.ReadAsync(head, ct);
        if (rule.Signatures.Length > 0 && !rule.Signatures.Any(sig => read >= sig.Length && head.AsSpan(0, sig.Length).SequenceEqual(sig)))
            throw AppException.BadRequest("Dosya içeriği uzantısıyla uyuşmuyor.");
        input.Position = 0;

        var id = Guid.NewGuid();
        var rel = Path.Combine(DateTime.UtcNow.ToString("yyyy-MM"), id.ToString("N") + ext.ToLowerInvariant());
        var full = Path.Combine(Root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using (var output = File.Create(full)) await input.CopyToAsync(output, ct);

        var safeName = Path.GetFileName(file.FileName);
        if (safeName.Length > 200) safeName = safeName[^200..];
        var stored = new StoredFile { Id = id, FileName = safeName, ContentType = rule.Mime, Size = file.Length, StoragePath = rel, UploadedById = uploaderId };
        db.Files.Add(stored);
        logger.LogInformation("Dosya yüklendi {FileId} {Size} bayt", id, file.Length);
        return stored;
    }

    public Stream Open(StoredFile file)
    {
        var full = Path.GetFullPath(Path.Combine(Root, file.StoragePath));
        if (!full.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) throw AppException.Forbidden();
        if (!File.Exists(full)) throw AppException.NotFound("Dosya");
        return File.OpenRead(full);
    }
}
