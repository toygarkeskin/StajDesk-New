using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Email;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Worker;

/// <summary>
/// Gerçek zamanlı kanal: bildirimi Redis pub/sub ile API'ye iletir; API bunu SignalR ile kullanıcının açık oturumlarına gönderir.
/// Redis kapalıysa sessizce atlanır (kullanıcı bildirimi bir sonraki sorguda görür).
/// </summary>
public sealed class RealtimeChannel(StackExchange.Redis.IConnectionMultiplexer redis, ILogger<RealtimeChannel> logger) : INotificationChannel
{
    public const string RedisChannel = "stajdesk:realtime";
    public string Name => "realtime";

    public async Task SendAsync(IReadOnlyList<OutgoingNotification> list, CancellationToken ct)
    {
        if (list.Count == 0) return;
        try
        {
            var sub = redis.GetSubscriber();
            foreach (var n in list)
            {
                var payload = JsonSerializer.Serialize(new RealtimeEnvelope(n.UserId, "notification", new { n.Title, n.Message, n.Kind, n.Link }), MessagingTopology.Json);
                await sub.PublishAsync(StackExchange.Redis.RedisChannel.Literal(RedisChannel), payload);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Gerçek zamanlı bildirim yayınlanamadı: {Error}", ex.Message);
        }
    }
}
