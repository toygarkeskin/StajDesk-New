using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public record Ref(Guid Id, string FullName);

public record DeptRef(int Id, string Name);

public record RoleRef(string Code, string Name);

public record MeDto(Guid Id, string FirstName, string LastName, string FullName, string Email, string? EmployeeNo, DeptRef? Department, string? JobTitle,
    Ref? Manager, string? Phone, DateOnly? BirthDate, DateOnly? HireDate, string? Location, string? Bio, EmploymentType EmploymentType,
    IReadOnlyList<RoleRef> Roles, IReadOnlyList<string> Permissions, bool MustChangePassword);

public record LoginRequest(string Email, string Password);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UpdateProfileRequest(string? Phone, string? Location, string? Bio);

public record AuthResponse(string AccessToken, DateTime ExpiresAt, MeDto User);

public record UserListItem(Guid Id, string FullName, string Email, string? EmployeeNo, DeptRef? Department, string? JobTitle, IReadOnlyList<string> Roles,
    bool IsActive, bool MustChangePassword, DateTime? LastLoginAt, DateTime CreatedAt);

public record UserDetail(Guid Id, string FirstName, string LastName, string FullName, string Email, string? EmployeeNo, int? DepartmentId, string? DepartmentName,
    int? JobTitleId, string? JobTitle, Ref? Manager, string? Phone, DateOnly? BirthDate, DateOnly? HireDate, EmploymentType EmploymentType, string? Location,
    bool IsActive, DateTime? DeactivatedAt, string? DeactivationReason, bool MustChangePassword, DateTime? LastLoginAt, DateTime CreatedAt,
    IReadOnlyList<RoleRef> Roles, IReadOnlyList<string> Groups, int AssignedAssetCount, int OpenTicketCount);

public record SaveUserRequest(string FirstName, string LastName, string Email, string? EmployeeNo, int? DepartmentId, int? JobTitleId, Guid? ManagerId,
    string? Phone, DateOnly? BirthDate, DateOnly? HireDate, EmploymentType EmploymentType, string? Location, List<string>? RoleCodes);

public record CreatedUserResponse(UserDetail User, string TemporaryPassword);

public record SetRolesRequest(List<string> RoleCodes);

public record DeactivateRequest(string Reason);

public record DirectoryEntry(Guid Id, string FullName, string Email, string? Phone, string? JobTitle, DeptRef? Department, string? Location, Ref? Manager, DateOnly? HireDate);

public record BirthdayEntry(Guid Id, string FullName, string? JobTitle, string? Department, int Month, int Day, bool IsToday);

public record RoleDto(int Id, string Code, string Name, string? Description, bool IsSystem, IReadOnlyList<string> Permissions, int UserCount);

public record SaveRoleRequest(string Code, string Name, string? Description, List<string> Permissions);

public record PermissionDto(string Code, string Group, string Label);

public record DepartmentDto(int Id, string Name, string? Code, DepartmentType Type, int? ParentId, Ref? Manager, int UserCount, bool IsActive);

public record SaveDepartmentRequest(string Name, string? Code, DepartmentType Type, int? ParentId, Guid? ManagerId, bool IsActive = true);

public record JobTitleDto(int Id, string Name, int UserCount);

public record SaveJobTitleRequest(string Name);

public record GroupMemberDto(Guid UserId, string FullName, string? JobTitle, bool IsLead);

public record GroupDto(int Id, string Name, string? Description, DeptRef? Department, bool IsSupportGroup, IReadOnlyList<GroupMemberDto> Members, int OpenTicketCount);

public record SaveGroupRequest(string Name, string? Description, int? DepartmentId, bool IsSupportGroup, List<Guid>? MemberIds, List<Guid>? LeadIds);

public record AccountRequestDto(int Id, string FirstName, string LastName, string Email, string? EmployeeNo, DeptRef Department, string? JobTitle, Ref? Manager,
    DateOnly StartDate, EmploymentType EmploymentType, string? Phone, string? Note, AccountRequestStatus Status, Ref RequestedBy, DateTime CreatedAt,
    Ref? ProcessedBy, DateTime? ProcessedAt, string? RejectReason, Guid? CreatedUserId);

public record CreateAccountRequest(string FirstName, string LastName, string Email, string? EmployeeNo, int DepartmentId, int? JobTitleId, Guid? ManagerId,
    DateOnly StartDate, EmploymentType EmploymentType, string? Phone, string? Note);

public record RejectRequest(string Reason);

public record AuditLogDto(long Id, Guid? UserId, string? UserName, string Action, string EntityType, string? EntityId, string? Changes, string? IpAddress, DateTime CreatedAt);
