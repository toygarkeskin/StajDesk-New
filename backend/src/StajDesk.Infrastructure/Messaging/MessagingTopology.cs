using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Infrastructure.Messaging;

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
