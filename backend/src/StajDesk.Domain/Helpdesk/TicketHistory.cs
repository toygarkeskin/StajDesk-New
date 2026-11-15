namespace StajDesk.Domain;

public class TicketHistory
{
    public long Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }
    public required string Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
