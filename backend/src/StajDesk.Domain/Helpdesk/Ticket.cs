namespace StajDesk.Domain;

public class Ticket : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketType Type { get; set; } = TicketType.Incident;

    public int CategoryId { get; set; }
    public TicketCategory Category { get; set; } = null!;
    public Guid RequesterId { get; set; }
    public User Requester { get; set; } = null!;
    /// <summary>Talep sahibinin talep anındaki departmanı (raporlama ve yönetici görünürlüğü için).</summary>
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? GroupId { get; set; }
    public Group? Group { get; set; }
    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }
    public int? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public int? AppliedRuleId { get; set; }

    public DateTime DueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public ICollection<TicketComment> Comments { get; set; } = [];
    public ICollection<TicketHistory> History { get; set; } = [];
    public ICollection<TicketAttachment> Attachments { get; set; } = [];

    public bool IsOverdue(DateTime now) => Status is not (TicketStatus.Resolved or TicketStatus.Closed) && DueAt < now;
}
