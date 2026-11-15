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

public record OutgoingNotification(Guid UserId, string Title, string Message, string Kind, string? Link, Guid EventId);

public record RealtimeEnvelope(Guid UserId, string Type, object Data);
