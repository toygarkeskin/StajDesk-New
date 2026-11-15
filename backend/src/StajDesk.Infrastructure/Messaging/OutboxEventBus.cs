using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Infrastructure.Messaging;

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
