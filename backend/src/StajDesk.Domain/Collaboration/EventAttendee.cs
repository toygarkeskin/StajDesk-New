namespace StajDesk.Domain;

public class EventAttendee
{
    public int EventId { get; set; }
    public CompanyEvent Event { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
