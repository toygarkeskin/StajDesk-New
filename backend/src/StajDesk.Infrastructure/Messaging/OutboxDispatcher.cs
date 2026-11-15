using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Infrastructure.Messaging;

/// <summary>Outbox'taki yayınlanmamış olayları belirli aralıklarla RabbitMQ'ya aktaran yardımcı.</summary>
public sealed class OutboxDispatcher(AppDbContext db, RabbitMqPublisher publisher, ILogger<OutboxDispatcher> logger)
{
    public async Task<int> DispatchAsync(int batchSize, CancellationToken ct)
    {
        var pending = await db.Outbox.Where(m => m.PublishedAt == null && m.Attempts < 20)
            .OrderBy(m => m.Id).Take(batchSize).ToListAsync(ct);
        var sent = 0;
        foreach (var msg in pending)
        {
            try
            {
                await publisher.PublishAsync(msg.RoutingKey, msg.EventId, System.Text.Encoding.UTF8.GetBytes(msg.Payload), ct);
                msg.PublishedAt = DateTime.UtcNow;
                sent++;
                logger.LogInformation("Olay yayınlandı {RoutingKey} {EventId}", msg.RoutingKey, msg.EventId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                msg.Attempts++;
                msg.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                logger.LogWarning("Olay yayınlanamadı (deneme {Attempt}) {RoutingKey}: {Error}", msg.Attempts, msg.RoutingKey, ex.Message);
                break; // RabbitMQ erişilemiyorsa sıradakileri denemeden çık; bir sonraki turda tekrar denenir
            }
        }
        if (pending.Count > 0) await db.SaveChangesAsync(ct);
        return sent;
    }
}
