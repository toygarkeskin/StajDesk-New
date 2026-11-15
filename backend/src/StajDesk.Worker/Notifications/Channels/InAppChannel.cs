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

/// <summary>Bildirim merkezine (PostgreSQL) yazar. Aynı olay iki kez gelirse tekrar kayıt oluşturmaz.</summary>
public sealed class InAppChannel(AppDbContext db) : INotificationChannel
{
    public string Name => "in-app";

    public async Task SendAsync(IReadOnlyList<OutgoingNotification> list, CancellationToken ct)
    {
        if (list.Count == 0) return;
        var eventId = list[0].EventId;
        var existing = await db.Notifications.Where(n => n.SourceEventId == eventId).Select(n => n.UserId).ToListAsync(ct);
        foreach (var n in list.Where(n => !existing.Contains(n.UserId)))
            db.Notifications.Add(new Notification { UserId = n.UserId, Title = n.Title, Message = n.Message, Kind = n.Kind, Link = n.Link, SourceEventId = n.EventId });
        await db.SaveChangesAsync(ct);
    }
}
