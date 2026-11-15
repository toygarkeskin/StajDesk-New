namespace StajDesk.Domain;

public class SoftwareLicense : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Vendor { get; set; }
    public string? Version { get; set; }
    public int Seats { get; set; }
    public DateOnly? ExpiresAt { get; set; }
    public decimal? Cost { get; set; }
    public string? Notes { get; set; }
    public ICollection<SoftwareInstallation> Installations { get; set; } = [];
}
