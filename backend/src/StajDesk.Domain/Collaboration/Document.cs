namespace StajDesk.Domain;

public class Document : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string Category { get; set; } = "Genel";
    public Guid FileId { get; set; }
    public StoredFile File { get; set; } = null!;
    /// <summary>Boşsa tüm çalışanlar görebilir; doluysa yalnızca bu departman.</summary>
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
    public int Downloads { get; set; }
}
