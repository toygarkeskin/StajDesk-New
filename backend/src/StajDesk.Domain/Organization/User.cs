namespace StajDesk.Domain;

public class User : Entity, IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? EmployeeNo { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string FullName => $"{FirstName} {LastName}";
    public required string Email { get; set; }
    public string PasswordHash { get; set; } = "";
    public bool MustChangePassword { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime? DeactivatedAt { get; set; }
    public string? DeactivationReason { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? JobTitleId { get; set; }
    public JobTitle? JobTitle { get; set; }
    public Guid? ManagerId { get; set; }
    public User? Manager { get; set; }

    public string? Phone { get; set; }
    public DateOnly? BirthDate { get; set; }
    public DateOnly? HireDate { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public string? Location { get; set; }
    public string? Bio { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginCount { get; set; }

    public ICollection<UserRole> Roles { get; set; } = [];
    public ICollection<GroupMember> Groups { get; set; } = [];
}
