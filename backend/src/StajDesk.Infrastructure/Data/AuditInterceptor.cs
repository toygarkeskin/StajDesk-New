using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using StajDesk.Domain;

namespace StajDesk.Infrastructure.Data;

/// <summary>
/// SaveChanges sırasında IAuditable varlıkların değişikliklerini audit_logs tablosuna yazar
/// ve UpdatedAt alanını günceller. Parola özeti gibi hassas alanlar kayda alınmaz.
/// </summary>
public sealed class AuditInterceptor(ICurrentUser currentUser, TimeProvider clock) : SaveChangesInterceptor
{
    private static readonly HashSet<string> Sensitive = ["PasswordHash", "TokenHash", "ReplacedByHash", "FailedLoginCount", "UpdatedAt", "CreatedAt"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        if (eventData.Context is AppDbContext db) Capture(db);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is AppDbContext db) Capture(db);
        return base.SavingChanges(eventData, result);
    }

    private void Capture(AppDbContext db)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var logs = new List<(EntityEntry Entry, AuditLog Log)>();

        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is Entity e && entry.State == EntityState.Modified) e.UpdatedAt = now;
            if (entry.Entity is not IAuditable || entry.State is EntityState.Unchanged or EntityState.Detached) continue;

            var action = entry.State switch { EntityState.Added => "Create", EntityState.Deleted => "Delete", _ => "Update" };
            var changes = new Dictionary<string, object?>();
            foreach (var p in entry.Properties)
            {
                if (Sensitive.Contains(p.Metadata.Name)) continue;
                if (entry.State == EntityState.Modified && (!p.IsModified || Equals(p.OriginalValue, p.CurrentValue))) continue;
                if (entry.State == EntityState.Added && p.CurrentValue is null) continue;
                changes[p.Metadata.Name] = entry.State switch
                {
                    EntityState.Modified => new { from = p.OriginalValue, to = p.CurrentValue },
                    EntityState.Deleted => p.OriginalValue,
                    _ => p.CurrentValue,
                };
            }
            if (entry.State == EntityState.Modified && changes.Count == 0) continue;

            logs.Add((entry, new AuditLog
            {
                UserId = currentUser.UserId,
                UserName = currentUser.UserName,
                IpAddress = currentUser.IpAddress,
                Action = action,
                EntityType = entry.Metadata.ClrType.Name,
                Changes = JsonSerializer.Serialize(changes, Json),
                CreatedAt = now,
            }));
        }

        foreach (var (entry, log) in logs)
        {
            // Eklenen kayıtların int/long anahtarı SaveChanges'ten önce bilinmez; geçici değer yerine
            // kimliği bilinen anahtarları yazıyoruz (Guid, mevcut kayıtlar).
            var key = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (key is not null)
            {
                var prop = entry.Property(key.Name);
                if (!prop.IsTemporary) log.EntityId = prop.CurrentValue?.ToString();
            }
            db.AuditLogs.Add(log);
        }
    }
}
