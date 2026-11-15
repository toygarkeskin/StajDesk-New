using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace StajDesk.Api.Common;

/// <summary>
/// Worker'ın Redis "stajdesk:realtime" kanalına yayınladığı olayları dinler ve ilgili kullanıcının açık
/// SignalR bağlantılarına iletir. Böylece API birden çok örnekle çalışsa da her örnek kendi bağlantılarına iletir.
/// Redis kapalıysa istemciler periyodik sorguya geri düşer; bildirim kaybolmaz (kalıcı kayıt PostgreSQL'dedir).
/// </summary>
public sealed class RealtimeRelayService(IConnectionMultiplexer redis, IHubContext<NotificationHub> hub, ILogger<RealtimeRelayService> logger) : BackgroundService
{
    public const string Channel = "stajdesk:realtime";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                var queue = await redis.GetSubscriber().SubscribeAsync(RedisChannel.Literal(Channel));
                queue.OnMessage(ForwardAsync);
                logger.LogInformation("Gerçek zamanlı bildirim kanalı dinleniyor");
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Redis aboneliği kurulamadı (deneme {Attempt}): {Error}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, attempt * 3)), stoppingToken);
            }
        }
    }

    private async Task ForwardAsync(ChannelMessage message)
    {
        try
        {
            using var doc = JsonDocument.Parse((string)message.Message!);
            var root = doc.RootElement;
            var userId = root.GetProperty("userId").GetString();
            var type = root.GetProperty("type").GetString();
            if (string.IsNullOrEmpty(userId) || type is not ("notification" or "message")) return;
            await hub.Clients.User(userId).SendAsync(type, root.GetProperty("data").Clone());
        }
        catch (Exception ex)
        {
            logger.LogWarning("Gerçek zamanlı olay iletilemedi: {Error}", ex.Message);
        }
    }
}
