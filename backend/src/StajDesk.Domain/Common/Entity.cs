namespace StajDesk.Domain;

/// <summary>Tüm varlıkların ortak zaman damgaları.</summary>
public abstract class Entity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
