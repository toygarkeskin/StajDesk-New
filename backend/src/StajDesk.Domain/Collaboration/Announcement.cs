namespace StajDesk.Domain;

public class Announcement : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Summary { get; set; }
    public required string Body { get; set; }
    public AnnouncementKind Kind { get; set; }
    /// <summary>Genel tanıtım sitesinde gösterilir mi?</summary>
    public bool IsPublic { get; set; }
    public bool IsPinned { get; set; }
    public DateTime PublishAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
}
