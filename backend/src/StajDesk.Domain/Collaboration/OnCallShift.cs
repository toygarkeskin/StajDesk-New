namespace StajDesk.Domain;

public class OnCallShift : Entity, IAuditable
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Team { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? Note { get; set; }
}
