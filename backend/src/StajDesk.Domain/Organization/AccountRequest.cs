namespace StajDesk.Domain;

/// <summary>İK'nın yeni personel için açtığı hesap talebi. Hesabı yalnızca IT/Admin oluşturur.</summary>
public class AccountRequest : Entity, IAuditable
{
    public int Id { get; set; }
    public Guid RequestedById { get; set; }
    public User RequestedBy { get; set; } = null!;
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public string? EmployeeNo { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int? JobTitleId { get; set; }
    public JobTitle? JobTitle { get; set; }
    public Guid? ManagerId { get; set; }
    public User? Manager { get; set; }
    public DateOnly StartDate { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public string? Phone { get; set; }
    public string? Note { get; set; }
    public AccountRequestStatus Status { get; set; }
    public Guid? ProcessedById { get; set; }
    public User? ProcessedBy { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RejectReason { get; set; }
    public Guid? CreatedUserId { get; set; }
}
