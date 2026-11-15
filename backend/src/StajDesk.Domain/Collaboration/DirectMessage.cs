namespace StajDesk.Domain;

public class DirectMessage
{
    public long Id { get; set; }
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public Guid RecipientId { get; set; }
    public User Recipient { get; set; } = null!;
    public required string Body { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
