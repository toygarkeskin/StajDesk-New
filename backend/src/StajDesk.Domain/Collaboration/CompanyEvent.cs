namespace StajDesk.Domain;

public class CompanyEvent : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string Category { get; set; } = "Kurumsal";
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsPublic { get; set; }
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<EventAttendee> Attendees { get; set; } = [];
}
