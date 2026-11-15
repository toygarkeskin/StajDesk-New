namespace StajDesk.Domain;

public class RefreshToken
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
    public string? CreatedByIp { get; set; }
    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
