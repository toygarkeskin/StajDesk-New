namespace StajDesk.Domain;

public class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long Size { get; set; }
    public required string StoragePath { get; set; }
    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
