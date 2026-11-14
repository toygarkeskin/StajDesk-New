namespace StajDesk.Domain;

public enum DepartmentType { HeadOffice = 0, Store = 1, Region = 2 }
public enum EmploymentType { FullTime = 0, PartTime = 1, Intern = 2 }
public enum AccountRequestStatus { Pending = 0, Approved = 1, Rejected = 2 }

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

public class JobTitle : Entity
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

/// <summary>Sistem rolü. Organizasyonel unvandan bağımsızdır; yetkiler izin kodlarıyla tanımlanır.</summary>
public class Role : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public List<string> Permissions { get; set; } = [];
    public ICollection<UserRole> Users { get; set; } = [];
}

public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

/// <summary>Destek grupları ve ekipler (ör. IT Destek L1, Ağ ve Altyapı).</summary>
public class Group : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public bool IsSupportGroup { get; set; }
    public ICollection<GroupMember> Members { get; set; } = [];
}

public class GroupMember
{
    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public bool IsLead { get; set; }
}

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

public class RefreshToken
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }
    public string? CreatedByIp { get; set; }
    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}

/// <summary>
/// Giriş doğrulama kodu (e-posta ile ikinci adım). Kod düz metin saklanmaz; yalnızca özet tutulur.
/// Kısa ömürlüdür, deneme ve yeniden gönderme sayısı sınırlıdır, tek kullanımlıktır.
/// </summary>
public class LoginChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string CodeHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastSentAt { get; set; }
    public int Attempts { get; set; }
    public int SendCount { get; set; } = 1;
    public DateTime? ConsumedAt { get; set; }
    public string? Ip { get; set; }
}

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

public class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Changes { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
