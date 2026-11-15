namespace StajDesk.Domain;

public class WorkTask : Entity, IAuditable
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public WorkTaskStatus Status { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public DateOnly? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
}
