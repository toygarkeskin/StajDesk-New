namespace StajDesk.Domain;

/// <summary>
/// Giriş doğrulama kodu (e-posta ile ikinci adım). Kod düz metin saklanmaz; yalnızca özet tutulur.
/// Kısa ömürlüdür, deneme ve yeniden gönderme sayısı sınırlıdır, tek kullanımlıktır.
/// </summary>
public class LoginChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string CodeHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastSentAt { get; set; }
    public int Attempts { get; set; }
    public int SendCount { get; set; } = 1;
    public DateTime? ConsumedAt { get; set; }
    public string? Ip { get; set; }
}
