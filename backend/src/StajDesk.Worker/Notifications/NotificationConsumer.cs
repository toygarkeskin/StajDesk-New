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
/// stajdesk.notifications kuyruğunu dinler. Başarılı işlemde ack; ilk hatada bir kez yeniden kuyruğa alır,
/// tekrar hata olursa dead-letter kuyruğuna gönderir. Kuyruk kalıcı olduğu için Worker kapalıyken gelen
/// mesajlar kaybolmaz; Worker açıldığında işlenir.
/// </summary>
public sealed class NotificationConsumer(IOptions<RabbitMqOptions> options, IServiceScopeFactory scopes, ILogger<NotificationConsumer> logger) : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = options.Value.CreateFactory("stajdesk-worker");
        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await MessagingTopology.DeclareAsync(_channel, stoppingToken);
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, attempt * 3));
                logger.LogWarning("RabbitMQ'ya bağlanılamadı (deneme {Attempt}), {Delay} sn sonra tekrar: {Error}", attempt, delay.TotalSeconds, ex.Message);
                await Task.Delay(delay, stoppingToken);
            }
        }
        if (_channel is null) return;

        await _channel.BasicQosAsync(0, prefetchCount: 10, global: false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += HandleAsync;
        await _channel.BasicConsumeAsync(MessagingTopology.NotificationQueue, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Worker '{Queue}' kuyruğunu dinliyor", MessagingTopology.NotificationQueue);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
    }

    private async Task HandleAsync(object sender, BasicDeliverEventArgs ea)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object> { ["RoutingKey"] = ea.RoutingKey, ["MessageId"] = ea.BasicProperties.MessageId ?? "" });
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var router = scope.ServiceProvider.GetRequiredService<NotificationRouter>();
            var json = MessagingTopology.Json;
            var body = ea.Body.Span;
            var count = ea.RoutingKey.Split('.')[0] switch
            {
                "ticket" => await router.HandleAsync(JsonSerializer.Deserialize<TicketEvent>(body, json)!),
                "workflow" => await router.HandleAsync(JsonSerializer.Deserialize<WorkflowEvent>(body, json)!),
                "account" => await router.HandleAsync(JsonSerializer.Deserialize<AccountEvent>(body, json)!),
                "notify" => await router.HandleAsync(JsonSerializer.Deserialize<DirectNotificationEvent>(body, json)!),
                _ => -1,
            };
            if (count < 0) logger.LogWarning("Bilinmeyen olay türü {RoutingKey}, atlandı", ea.RoutingKey);
            else logger.LogInformation("Olay işlendi {RoutingKey}: {Count} bildirim", ea.RoutingKey, count);
            await _channel!.BasicAckAsync(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            var requeue = !ea.Redelivered;
            logger.LogError(ex, "Olay işlenemedi {RoutingKey}; {Action}", ea.RoutingKey, requeue ? "yeniden denenecek" : "dead-letter kuyruğuna gönderildi");
            await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);
    }
}
