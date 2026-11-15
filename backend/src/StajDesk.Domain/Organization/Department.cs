namespace StajDesk.Domain;

public class Department : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Code { get; set; }
    public DepartmentType Type { get; set; }
    public int? ParentId { get; set; }
    public Department? Parent { get; set; }
    public Guid? ManagerId { get; set; }
    public User? Manager { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<User> Users { get; set; } = [];
}
