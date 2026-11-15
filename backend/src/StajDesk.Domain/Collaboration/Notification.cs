namespace StajDesk.Domain;

public class Notification
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Title { get; set; }
    public required string Message { get; set; }
    /// <summary>ticket | approval | success | warning | info | message | account</summary>
    public string Kind { get; set; } = "info";
    /// <summary>Frontend'de tıklanınca gidilecek yol (ör. /tickets/12).</summary>
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public Guid? SourceEventId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
