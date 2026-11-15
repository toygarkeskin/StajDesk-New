namespace StajDesk.Domain;

public class JobPosting : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string? Location { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public required string Description { get; set; }
    public string? Requirements { get; set; }
    public bool IsPublic { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateOnly? ClosesAt { get; set; }
}
