namespace StajDesk.Domain;

/// <summary>Zimmet (teslim) ve iade geçmişi.</summary>
public class AssetAssignment
{
    public long Id { get; set; }
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid AssignedById { get; set; }
    public User AssignedBy { get; set; } = null!;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReturnedAt { get; set; }
    public Guid? ReturnedById { get; set; }
    public User? ReturnedBy { get; set; }
    public string? Note { get; set; }
    public string? ReturnCondition { get; set; }
}
