using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Infrastructure.Messaging;

// ======================= Olay sözleşmeleri =======================
// API olayları yayınlar, Worker bunları kullanıcı bildirimlerine dönüştürür.

public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public Guid? ActorId { get; init; }
    public string? ActorName { get; init; }
}

/// <summary>Kind: created | status_changed | assigned | commented</summary>
public record TicketEvent(string Kind, int TicketId, string Title, string Priority, Guid RequesterId, Guid? AssigneeId, int? GroupId,
    string? OldValue = null, string? NewValue = null, bool IsInternal = false) : IntegrationEvent
{
    public string RoutingKey => $"ticket.{Kind}";
}

/// <summary>Kind: submitted | step_pending | approved | rejected | completed | cancelled | commented</summary>
public record WorkflowEvent(string Kind, int SubmissionId, string Number, string FormName, Guid SubmitterId,
    IReadOnlyList<Guid> RecipientIds, string? StepName = null, string? Comment = null) : IntegrationEvent
{
    public string RoutingKey => $"workflow.{Kind}";
}

/// <summary>Kind: request_created | account_created | account_deactivated</summary>
public record AccountEvent(string Kind, string FullName, Guid? UserId, IReadOnlyList<Guid> RecipientIds) : IntegrationEvent
{
    public string RoutingKey => $"account.{Kind}";
}

/// <summary>Duyuru, takdir, görev, mesaj gibi genel bildirimler.</summary>
public record DirectNotificationEvent(IReadOnlyList<Guid> RecipientIds, string Title, string Message, string Kind, string? Link) : IntegrationEvent
{
    public string RoutingKey => "notify.direct";
}

public static class MessagingTopology
{
    public const string Exchange = "stajdesk.events";
    public const string NotificationQueue = "stajdesk.notifications";
    public const string DeadLetterExchange = "stajdesk.events.dlx";
    public const string DeadLetterQueue = "stajdesk.notifications.dlq";

    public static readonly JsonSerializerOptions Json = CreateJson();

    private static JsonSerializerOptions CreateJson()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }

    public static async Task DeclareAsync(IChannel ch, CancellationToken ct = default)
    {
        await ch.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await ch.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Fanout, durable: true, cancellationToken: ct);
        await ch.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await ch.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, "", cancellationToken: ct);
        await ch.QueueDeclareAsync(NotificationQueue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = DeadLetterExchange }, cancellationToken: ct);
        foreach (var key in new[] { "ticket.*", "workflow.*", "account.*", "notify.*" })
            await ch.QueueBindAsync(NotificationQueue, Exchange, key, cancellationToken: ct);
    }
}

public class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string User { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    public ConnectionFactory CreateFactory(string clientName) => new()
    {
        HostName = Host, Port = Port, UserName = User, Password = Password, VirtualHost = VirtualHost,
        ClientProvidedName = clientName, AutomaticRecoveryEnabled = true, TopologyRecoveryEnabled = true,
    };
}

// ======================= Outbox =======================

/// <summary>
/// Olayı doğrudan RabbitMQ'ya göndermek yerine aynı veritabanı işleminde outbox tablosuna yazar.
/// Böylece RabbitMQ geçici olarak erişilemez olsa bile olay kaybolmaz.
/// </summary>
public interface IEventBus
{
    void Enqueue(IntegrationEvent evt);
}

public sealed class OutboxEventBus(AppDbContext db) : IEventBus
{
    public void Enqueue(IntegrationEvent evt)
    {
        var routingKey = evt switch
        {
            TicketEvent t => t.RoutingKey,
            WorkflowEvent w => w.RoutingKey,
            AccountEvent a => a.RoutingKey,
            DirectNotificationEvent d => d.RoutingKey,
            _ => throw new InvalidOperationException($"Bilinmeyen olay tipi: {evt.GetType().Name}"),
        };
        db.Outbox.Add(new OutboxMessage
        {
            EventId = evt.EventId,
            RoutingKey = routingKey,
            Payload = JsonSerializer.Serialize(evt, evt.GetType(), MessagingTopology.Json),
            CreatedAt = evt.OccurredAt,
        });
    }
}

/// <summary>Tek bir paylaşılan RabbitMQ bağlantısı ve yayın kanalı.</summary>
public sealed class RabbitMqPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqPublisher> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    private async Task<IChannel> ChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;
        if (_connection is not { IsOpen: true })
        {
            if (_connection is not null) await _connection.DisposeAsync();
            _connection = await options.Value.CreateFactory("stajdesk-api").CreateConnectionAsync(ct);
        }
        _channel = await _connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), ct);
        await MessagingTopology.DeclareAsync(_channel, ct);
        logger.LogInformation("RabbitMQ yayın kanalı açıldı");
        return _channel;
    }

    public async Task PublishAsync(string routingKey, Guid messageId, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var ch = await ChannelAsync(ct);
            var props = new BasicProperties
            {
                Persistent = true, ContentType = "application/json", MessageId = messageId.ToString(), Type = routingKey,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            };
            await ch.BasicPublishAsync(MessagingTopology.Exchange, routingKey, mandatory: false, props, body, ct);
        }
        catch
        {
            _channel = null;
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(uint Messages, uint Consumers)> InspectAsync(string queue, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var ok = await (await ChannelAsync(ct)).QueueDeclarePassiveAsync(queue, ct);
            return (ok.MessageCount, ok.ConsumerCount);
        }
        catch
        {
            _channel = null;
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }
}

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
