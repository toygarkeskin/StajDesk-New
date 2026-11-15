namespace StajDesk.Domain;

public class SoftwareInstallation
{
    public long Id { get; set; }
    public int LicenseId { get; set; }
    public SoftwareLicense License { get; set; } = null!;
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
}
