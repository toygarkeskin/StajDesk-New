namespace StajDesk.Domain;

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
