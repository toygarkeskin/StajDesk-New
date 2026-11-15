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

public interface INotificationChannel
{
    string Name { get; }
    Task SendAsync(IReadOnlyList<OutgoingNotification> notifications, CancellationToken ct);
}
