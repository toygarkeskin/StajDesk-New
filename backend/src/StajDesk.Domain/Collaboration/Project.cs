namespace StajDesk.Domain;

public class Project : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public ProjectStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public ICollection<WorkTask> Tasks { get; set; } = [];
}
