using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Infrastructure.Messaging;

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
