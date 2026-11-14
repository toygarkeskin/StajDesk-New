namespace StajDesk.Domain;

public enum AssetType { Computer = 0, Laptop = 1, Monitor = 2, Printer = 3, NetworkDevice = 4, Phone = 5, Server = 6, Peripheral = 7, HandheldTerminal = 8 }
public enum AssetStatus { InUse = 0, InStock = 1, InRepair = 2, Retired = 3 }

public class Asset : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string AssetTag { get; set; }
    public AssetType Type { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.InStock;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string? Location { get; set; }
    public Guid? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyEndsAt { get; set; }
    public decimal? PurchaseCost { get; set; }
    public string? Notes { get; set; }
    public ICollection<AssetAssignment> Assignments { get; set; } = [];
    public ICollection<SoftwareInstallation> Software { get; set; } = [];
    public ICollection<Ticket> Tickets { get; set; } = [];
}

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

public class SoftwareInstallation
{
    public long Id { get; set; }
    public int LicenseId { get; set; }
    public SoftwareLicense License { get; set; } = null!;
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public DateTime InstalledAt { get; set; } = DateTime.UtcNow;
}
