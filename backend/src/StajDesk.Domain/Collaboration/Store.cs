namespace StajDesk.Domain;

public class Store : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? City { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid? ManagerId { get; set; }
    public User? Manager { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public StoreStatus Status { get; set; }
    public decimal? AreaSqm { get; set; }
}
