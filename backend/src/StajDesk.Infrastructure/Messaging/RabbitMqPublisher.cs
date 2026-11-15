using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Infrastructure.Messaging;

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
