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

// ============================================================ DTO'lar
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

public class UserQuery : PagedQuery
{
    public int? DepartmentId { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    /// <summary>Belirli çalışanlarla sınırla (rehber).</summary>
    public List<Guid>? Ids { get; set; }
}

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
public class AuditQuery : PagedQuery
{
    public string? EntityType { get; set; }
    public string? Action { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

// ============================================================ Doğrulayıcılar
public static class PasswordPolicy
{
    public const string Message = "Parola en az 10 karakter olmalı; büyük harf, küçük harf ve rakam içermelidir.";
    public static bool IsValid(string? p) => p is { Length: >= 10 } && p.Any(char.IsUpper) && p.Any(char.IsLower) && p.Any(char.IsDigit);

    public static string Generate()
    {
        const string upper = "ABCDEFGHJKLMNPRSTUVYZ", lower = "abcdefghijkmnprstuvyz", digits = "23456789", symbols = "!@#$%";
        var chars = new[] { Pick(upper), Pick(upper), Pick(lower), Pick(lower), Pick(lower), Pick(lower), Pick(digits), Pick(digits), Pick(digits), Pick(symbols), Pick(lower), Pick(upper) };
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
        static char Pick(string s) => s[RandomNumberGenerator.GetInt32(s.Length)];
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-posta zorunludur.").EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Parola zorunludur.");
    }
}

public class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Mevcut parolanızı giriniz.");
        RuleFor(x => x.NewPassword).Must(PasswordPolicy.IsValid).WithMessage(PasswordPolicy.Message)
            .NotEqual(x => x.CurrentPassword).WithMessage("Yeni parola mevcut paroladan farklı olmalıdır.");
    }
}

public class SaveUserValidator : AbstractValidator<SaveUserRequest>
{
    public SaveUserValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("Ad zorunludur.").MaximumLength(60);
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Soyad zorunludur.").MaximumLength(60);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Geçerli bir kurumsal e-posta adresi giriniz.").MaximumLength(200);
        RuleFor(x => x.DepartmentId).NotNull().WithMessage("Departman seçiniz.");
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.BirthDate).LessThan(DateOnly.FromDateTime(DateTime.Today.AddYears(-15))).When(x => x.BirthDate.HasValue).WithMessage("Doğum tarihi geçersiz.");
    }
}

public class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("Ad zorunludur.").MaximumLength(60);
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Soyad zorunludur.").MaximumLength(60);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Geçerli bir kurumsal e-posta adresi giriniz.");
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Departman seçiniz.");
        RuleFor(x => x.StartDate).GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddDays(-30))).WithMessage("Başlangıç tarihi geçmişte olamaz.");
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public class SaveRoleValidator : AbstractValidator<SaveRoleRequest>
{
    public SaveRoleValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches("^[A-Z_]{2,40}$").WithMessage("Rol kodu büyük harf ve alt çizgiden oluşmalıdır (ör. BOLGE_MUDURU).");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleForEach(x => x.Permissions).Must(p => Permissions.All.Any(a => a.Code == p)).WithMessage("Tanımsız izin kodu.");
    }
}

public class SaveDepartmentValidator : AbstractValidator<SaveDepartmentRequest>
{
    public SaveDepartmentValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Departman adı zorunludur.").MaximumLength(150);
}

public class SaveGroupValidator : AbstractValidator<SaveGroupRequest>
{
    public SaveGroupValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Grup adı zorunludur.").MaximumLength(100);
}

// ============================================================ Servisler

/// <summary>Kullanıcının yönetici olarak görebileceği departmanlar: kendi departmanı, yönettiği departmanlar ve doğrudan bağlılarının departmanları.</summary>
public class AccessScope(AppDbContext db)
{
    private HashSet<int>? _cache;

    public async Task<HashSet<int>> DepartmentIdsAsync(UserContext u, CancellationToken ct)
    {
        if (_cache is not null) return _cache;
        var managed = await db.Departments.Where(d => d.ManagerId == u.Id).Select(d => d.Id).ToListAsync(ct);
        var reports = await db.Users.Where(x => x.ManagerId == u.Id && x.DepartmentId != null).Select(x => x.DepartmentId!.Value).Distinct().ToListAsync(ct);
        _cache = [.. managed, .. reports];
        if (u.DepartmentId is { } d) _cache.Add(d);
        return _cache;
    }
}

public class AuthService(AppDbContext db, IPasswordHasher<User> hasher, JwtTokenService jwt, LoginRateLimiter limiter,
    IOptions<JwtOptions> jwtOptions, TimeProvider clock, LoginCodeService loginCodes, ILogger<AuthService> logger)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>
    /// 1. adım: e-posta + parola. Doğrulama kodu açıksa oturum açılmaz, e-postaya kod gönderilip meydan okuma döner;
    /// kapalıysa (ör. SMTP tanımsız) doğrudan oturum açılır.
    /// </summary>
    public async Task<(LoginResult Result, string? RefreshToken)> LoginAsync(LoginRequest req, string? ip, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await limiter.GetLockSecondsAsync(email) is { } wait)
            throw new AppException(HttpStatusCode.TooManyRequests, "Çok fazla deneme yaptınız, lütfen birkaç dakika sonra tekrar deneyiniz.", "TOO_MANY_ATTEMPTS") { RetryAfterSeconds = wait };

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
        {
            var attempts = await limiter.RegisterFailureAsync(email);
            logger.LogWarning("Hatalı giriş denemesi ({Attempt}/{Max}) IP {Ip}", attempts, LoginRateLimiter.MaxAttempts, ip);
            if (user is not null) { user.FailedLoginCount++; await db.SaveChangesAsync(ct); }
            throw AppException.Unauthorized("E-posta adresiniz veya parolanız hatalıdır.");
        }
        if (!user.IsActive)
        {
            logger.LogWarning("Pasif hesapla giriş denemesi {UserId}", user.Id);
            throw new AppException(HttpStatusCode.Forbidden, "Hesabınız devre dışı bırakılmıştır. Lütfen İnsan Kaynakları ile iletişime geçiniz.", "ACCOUNT_DISABLED");
        }

        await limiter.ResetAsync(email);
        user.FailedLoginCount = 0;
        if (loginCodes.IsActive)
        {
            await db.SaveChangesAsync(ct);
            return (new LoginResult(true, await loginCodes.CreateAsync(user, ip, ct), null), null);
        }
        var (auth, refresh) = await CompleteLoginAsync(user, ip, ct);
        return (new LoginResult(false, null, auth), refresh);
    }

    /// <summary>2. adım: e-postaya gelen kod doğrulanınca oturum açılır.</summary>
    public async Task<(AuthResponse Response, string RefreshToken)> VerifyLoginCodeAsync(VerifyLoginCodeRequest req, string? ip, CancellationToken ct)
    {
        var user = await loginCodes.VerifyAsync(req, ct);
        return await CompleteLoginAsync(user, ip, ct);
    }

    public Task<LoginChallengeDto> ResendLoginCodeAsync(Guid challengeId, CancellationToken ct) => loginCodes.ResendAsync(challengeId, ct);

    private async Task<(AuthResponse Response, string RefreshToken)> CompleteLoginAsync(User user, string? ip, CancellationToken ct)
    {
        user.LastLoginAt = Now;
        db.AuditLogs.Add(new AuditLog { UserId = user.Id, UserName = user.FullName, Action = "Login", EntityType = nameof(User), EntityId = user.Id.ToString(), IpAddress = ip, CreatedAt = Now });
        var refresh = IssueRefreshToken(user.Id, ip);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Kullanıcı giriş yaptı {UserId}", user.Id);
        return (await BuildAsync(user.Id, ct), refresh);
    }

    public async Task<(AuthResponse Response, string RefreshToken)> RefreshAsync(string? raw, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw AppException.Unauthorized("Oturumunuz sona erdi. Lütfen tekrar giriş yapınız.");
        var hash = JwtTokenService.HashToken(raw);
        var token = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                    ?? throw AppException.Unauthorized("Oturumunuz sona erdi. Lütfen tekrar giriş yapınız.");

        if (token.RevokedAt is not null)
        {
            // Daha önce kullanılmış (döndürülmüş) bir token tekrar geldi: çalınmış olabilir, tüm oturumları kapat.
            logger.LogWarning("Refresh token yeniden kullanımı tespit edildi {UserId}; tüm oturumlar kapatıldı", token.UserId);
            await RevokeAllAsync(token.UserId, ct);
            throw AppException.Unauthorized("Güvenlik nedeniyle oturumunuz kapatıldı. Lütfen tekrar giriş yapınız.");
        }
        if (!token.IsActive(Now) || !token.User.IsActive) throw AppException.Unauthorized("Oturumunuz sona erdi. Lütfen tekrar giriş yapınız.");

        var newRaw = IssueRefreshToken(token.UserId, ip, out var newHash);
        token.RevokedAt = Now;
        token.ReplacedByHash = newHash;
        await db.SaveChangesAsync(ct);
        return (await BuildAsync(token.UserId, ct), newRaw);
    }

    public async Task LogoutAsync(string? raw, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        var hash = JwtTokenService.HashToken(raw);
        await db.RefreshTokens.Where(t => t.TokenHash == hash && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, Now), ct);
    }

    public async Task<(AuthResponse Response, string RefreshToken)> ChangePasswordAsync(UserContext me, ChangePasswordRequest req, string? ip, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == me.Id, ct);
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.CurrentPassword) == PasswordVerificationResult.Failed)
            throw AppException.BadRequest("Mevcut parolanız hatalıdır.");
        user.PasswordHash = hasher.HashPassword(user, req.NewPassword);
        user.MustChangePassword = false;
        await RevokeAllAsync(user.Id, ct);
        db.AuditLogs.Add(new AuditLog { UserId = user.Id, UserName = user.FullName, Action = "PasswordChanged", EntityType = nameof(User), EntityId = user.Id.ToString(), IpAddress = ip, CreatedAt = Now });
        var refresh = IssueRefreshToken(user.Id, ip);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Parola değiştirildi {UserId}", user.Id);
        return (await BuildAsync(user.Id, ct), refresh);
    }

    public async Task<MeDto> UpdateProfileAsync(UserContext me, UpdateProfileRequest req, CancellationToken ct)
    {
        var user = await db.Users.FirstAsync(u => u.Id == me.Id, ct);
        user.Phone = req.Phone?.Trim();
        user.Location = req.Location?.Trim();
        user.Bio = req.Bio?.Trim() is { Length: > 500 } b ? b[..500] : req.Bio?.Trim();
        await db.SaveChangesAsync(ct);
        return await MeAsync(me.Id, ct);
    }

    public async Task<MeDto> MeAsync(Guid id, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.Department).Include(x => x.JobTitle).Include(x => x.Manager)
                    .Include(x => x.Roles).ThenInclude(r => r.Role).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw AppException.Unauthorized("Oturum geçersiz.");
        var perms = u.Roles.SelectMany(r => r.Role.Permissions).Distinct().OrderBy(p => p).ToList();
        return new MeDto(u.Id, u.FirstName, u.LastName, u.FullName, u.Email, u.EmployeeNo,
            u.Department is null ? null : new DeptRef(u.Department.Id, u.Department.Name), u.JobTitle?.Name,
            u.Manager is null ? null : new Ref(u.Manager.Id, u.Manager.FullName), u.Phone, u.BirthDate, u.HireDate, u.Location, u.Bio, u.EmploymentType,
            u.Roles.Select(r => new RoleRef(r.Role.Code, r.Role.Name)).ToList(), perms, u.MustChangePassword);
    }

    private async Task<AuthResponse> BuildAsync(Guid userId, CancellationToken ct)
    {
        var me = await MeAsync(userId, ct);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ct);
        var (token, expires) = jwt.CreateAccessToken(user, me.Roles.Select(r => r.Code), me.Permissions);
        return new AuthResponse(token, expires, me);
    }

    private string IssueRefreshToken(Guid userId, string? ip) => IssueRefreshToken(userId, ip, out _);

    private string IssueRefreshToken(Guid userId, string? ip, out string hash)
    {
        var (raw, h) = JwtTokenService.CreateRefreshToken();
        hash = h;
        db.RefreshTokens.Add(new RefreshToken { UserId = userId, TokenHash = h, ExpiresAt = Now.AddDays(jwtOptions.Value.RefreshTokenDays), CreatedByIp = ip, CreatedAt = Now });
        return raw;
    }

    private Task RevokeAllAsync(Guid userId, CancellationToken ct) =>
        db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, Now), ct);
}

public class UserService(AppDbContext db, IPasswordHasher<User> hasher, IEventBus events, ICacheService cache, TimeProvider clock, ILogger<UserService> logger)
{
    private static readonly string[] PrivilegedRoles = [RoleCodes.SuperAdmin, RoleCodes.Admin];

    public Task<PagedResult<UserListItem>> ListAsync(UserQuery q, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().AsQueryable();
        if (q.DepartmentId is { } d) query = query.Where(u => u.DepartmentId == d);
        if (q.IsActive is { } a) query = query.Where(u => u.IsActive == a);
        if (!string.IsNullOrWhiteSpace(q.Role)) query = query.Where(u => u.Roles.Any(r => r.Role.Code == q.Role));
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var t = q.Search.Like();
            query = query.Where(u => EF.Functions.ILike(u.FirstName + " " + u.LastName, t) || EF.Functions.ILike(u.Email, t) || (u.EmployeeNo != null && EF.Functions.ILike(u.EmployeeNo, t)));
        }
        return query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToPagedAsync(q, u => new UserListItem(u.Id, u.FirstName + " " + u.LastName, u.Email, u.EmployeeNo,
            u.Department == null ? null : new DeptRef(u.Department.Id, u.Department.Name), u.JobTitle == null ? null : u.JobTitle.Name,
            u.Roles.Select(r => r.Role.Code).ToList(), u.IsActive, u.MustChangePassword, u.LastLoginAt, u.CreatedAt), ct);
    }

    public async Task<UserDetail> GetAsync(Guid id, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().Include(x => x.Department).Include(x => x.JobTitle).Include(x => x.Manager)
            .Include(x => x.Roles).ThenInclude(r => r.Role).Include(x => x.Groups).ThenInclude(g => g.Group)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Kullanıcı");
        var assets = await db.Assets.CountAsync(a => a.AssignedToId == id, ct);
        var tickets = await db.Tickets.CountAsync(t => t.RequesterId == id && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved, ct);
        return new UserDetail(u.Id, u.FirstName, u.LastName, u.FullName, u.Email, u.EmployeeNo, u.DepartmentId, u.Department?.Name, u.JobTitleId, u.JobTitle?.Name,
            u.Manager is null ? null : new Ref(u.Manager.Id, u.Manager.FullName), u.Phone, u.BirthDate, u.HireDate, u.EmploymentType, u.Location,
            u.IsActive, u.DeactivatedAt, u.DeactivationReason, u.MustChangePassword, u.LastLoginAt, u.CreatedAt,
            u.Roles.Select(r => new RoleRef(r.Role.Code, r.Role.Name)).ToList(), u.Groups.Select(g => g.Group.Name).ToList(), assets, tickets);
    }

    public async Task<CreatedUserResponse> CreateAsync(SaveUserRequest req, UserContext actor, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) throw AppException.Conflict("Bu e-posta adresiyle kayıtlı bir hesap bulunmaktadır.");
        if (req.EmployeeNo is { } no && await db.Users.AnyAsync(u => u.EmployeeNo == no, ct)) throw AppException.Conflict("Bu personel numarası kullanılmaktadır.");

        var user = new User { FirstName = req.FirstName.Trim(), LastName = req.LastName.Trim(), Email = email, MustChangePassword = true };
        await ApplyAsync(user, req, ct);
        var temp = PasswordPolicy.Generate();
        user.PasswordHash = hasher.HashPassword(user, temp);
        await SetRolesInternalAsync(user, req.RoleCodes is { Count: > 0 } r ? r : [RoleCodes.Employee], actor, ct);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Hesap oluşturuldu {UserId} (işlemi yapan {ActorId})", user.Id, actor.Id);
        return new CreatedUserResponse(await GetAsync(user.Id, ct), temp);
    }

    public async Task<UserDetail> UpdateAsync(Guid id, SaveUserRequest req, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound("Kullanıcı");
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != id, ct)) throw AppException.Conflict("Bu e-posta adresi başka bir hesapta kullanılıyor.");
        if (req.ManagerId == id) throw AppException.BadRequest("Kullanıcı kendi yöneticisi olamaz.");
        user.FirstName = req.FirstName.Trim();
        user.LastName = req.LastName.Trim();
        user.Email = email;
        await ApplyAsync(user, req, ct);
        await db.SaveChangesAsync(ct);
        await cache.RemoveByPrefixAsync(CacheKeys.StatsPrefix);
        return await GetAsync(id, ct);
    }

    public async Task<UserDetail> SetRolesAsync(Guid id, SetRolesRequest req, UserContext actor, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Roles).ThenInclude(r => r.Role).FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound("Kullanıcı");
        if (id == actor.Id && user.Roles.Any(r => PrivilegedRoles.Contains(r.Role.Code)) && !req.RoleCodes.Any(PrivilegedRoles.Contains))
            throw AppException.BadRequest("Kendi yönetici rolünüzü kaldıramazsınız.");
        await SetRolesInternalAsync(user, req.RoleCodes, actor, ct);
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Rol değişikliği {UserId}: {Roles} (işlemi yapan {ActorId})", id, string.Join(",", req.RoleCodes), actor.Id);
        return await GetAsync(id, ct);
    }

    public async Task<UserDetail> DeactivateAsync(Guid id, DeactivateRequest req, UserContext actor, CancellationToken ct)
    {
        if (id == actor.Id) throw AppException.BadRequest("Kendi hesabınızı devre dışı bırakamazsınız.");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound("Kullanıcı");
        if (!user.IsActive) throw AppException.BadRequest("Hesap zaten devre dışı.");
        user.IsActive = false;
        user.DeactivatedAt = clock.GetUtcNow().UtcDateTime;
        user.DeactivationReason = string.IsNullOrWhiteSpace(req.Reason) ? "İşten ayrılış" : req.Reason.Trim();
        await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
        var itStaff = await db.Users.Where(u => u.IsActive && u.Roles.Any(r => r.Role.Code == RoleCodes.It)).Select(u => u.Id).ToListAsync(ct);
        events.Enqueue(new AccountEvent("account_deactivated", user.FullName, user.Id, itStaff) { ActorId = actor.Id, ActorName = actor.Name });
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Hesap devre dışı bırakıldı {UserId} (işlemi yapan {ActorId})", id, actor.Id);
        return await GetAsync(id, ct);
    }

    public async Task<UserDetail> ActivateAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound("Kullanıcı");
        user.IsActive = true;
        user.DeactivatedAt = null;
        user.DeactivationReason = null;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<string> ResetPasswordAsync(Guid id, UserContext actor, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw AppException.NotFound("Kullanıcı");
        var temp = PasswordPolicy.Generate();
        user.PasswordHash = hasher.HashPassword(user, temp);
        user.MustChangePassword = true;
        await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
        db.AuditLogs.Add(new AuditLog { UserId = actor.Id, UserName = actor.Name, Action = "PasswordReset", EntityType = nameof(User), EntityId = id.ToString(), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Parola sıfırlandı {UserId} (işlemi yapan {ActorId})", id, actor.Id);
        return temp;
    }

    public async Task<User> CreateFromRequestAsync(AccountRequest r, string tempPassword, UserContext actor, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Email == r.Email, ct)) throw AppException.Conflict("Bu e-posta adresiyle kayıtlı bir hesap zaten var.");
        var user = new User
        {
            FirstName = r.FirstName, LastName = r.LastName, Email = r.Email, EmployeeNo = r.EmployeeNo, DepartmentId = r.DepartmentId, JobTitleId = r.JobTitleId,
            ManagerId = r.ManagerId, HireDate = r.StartDate, EmploymentType = r.EmploymentType, Phone = r.Phone, MustChangePassword = true,
        };
        user.PasswordHash = hasher.HashPassword(user, tempPassword);
        await SetRolesInternalAsync(user, [RoleCodes.Employee], actor, ct);
        db.Users.Add(user);
        var entitlement = LeaveRules.AnnualEntitlement(r.StartDate, DateOnly.FromDateTime(DateTime.UtcNow));
        db.LeaveBalances.Add(new LeaveBalance { User = user, Year = DateTime.UtcNow.Year, Entitled = entitlement });
        return user;
    }

    private async Task ApplyAsync(User user, SaveUserRequest req, CancellationToken ct)
    {
        if (req.DepartmentId is { } d && !await db.Departments.AnyAsync(x => x.Id == d, ct)) throw AppException.BadRequest("Departman bulunamadı.");
        if (req.JobTitleId is { } t && !await db.JobTitles.AnyAsync(x => x.Id == t, ct)) throw AppException.BadRequest("Unvan bulunamadı.");
        if (req.ManagerId is { } m && !await db.Users.AnyAsync(x => x.Id == m && x.IsActive, ct)) throw AppException.BadRequest("Yönetici bulunamadı.");
        user.EmployeeNo = string.IsNullOrWhiteSpace(req.EmployeeNo) ? user.EmployeeNo : req.EmployeeNo.Trim();
        user.DepartmentId = req.DepartmentId;
        user.JobTitleId = req.JobTitleId;
        user.ManagerId = req.ManagerId;
        user.Phone = req.Phone?.Trim();
        user.BirthDate = req.BirthDate;
        user.HireDate = req.HireDate;
        user.EmploymentType = req.EmploymentType;
        user.Location = req.Location?.Trim();
    }

    private async Task SetRolesInternalAsync(User user, List<string> codes, UserContext actor, CancellationToken ct)
    {
        codes = codes.Append(RoleCodes.Employee).Distinct().ToList();
        if (codes.Any(PrivilegedRoles.Contains) && !actor.HasRole(RoleCodes.SuperAdmin) && !actor.Has(Permissions.RolesManage))
            throw AppException.Forbidden("Admin ve Super Admin rollerini yalnızca Super Admin atayabilir.");
        var roles = await db.Roles.Where(r => codes.Contains(r.Code)).ToListAsync(ct);
        if (roles.Count != codes.Count) throw AppException.BadRequest("Tanımsız rol kodu.");
        user.Roles.Clear();
        foreach (var r in roles) user.Roles.Add(new UserRole { Role = r, User = user });
    }
}

public class OrganizationService(AppDbContext db, ICacheService cache)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public Task<List<DepartmentDto>> DepartmentsAsync(CancellationToken ct) =>
        cache.GetOrSetAsync(CacheKeys.Departments, () => db.Departments.AsNoTracking().OrderBy(d => d.Type).ThenBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Code, d.Type, d.ParentId, d.Manager == null ? null : new Ref(d.Manager.Id, d.Manager.FirstName + " " + d.Manager.LastName),
                d.Users.Count(u => u.IsActive), d.IsActive)).ToListAsync(ct), Ttl);

    public async Task<DepartmentDto> SaveDepartmentAsync(int? id, SaveDepartmentRequest req, CancellationToken ct)
    {
        var name = req.Name.Trim();
        if (await db.Departments.AnyAsync(d => d.Name == name && d.Id != id, ct)) throw AppException.Conflict("Bu isimde bir departman zaten var.");
        Department d;
        if (id is { } existing) d = await db.Departments.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw AppException.NotFound("Departman");
        else db.Departments.Add(d = new Department { Name = name });
        if (req.ParentId == id && id is not null) throw AppException.BadRequest("Departman kendi üst birimi olamaz.");
        d.Name = name; d.Code = req.Code?.Trim(); d.Type = req.Type; d.ParentId = req.ParentId; d.ManagerId = req.ManagerId; d.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Departments);
        return (await DepartmentsAsync(ct)).First(x => x.Id == d.Id);
    }

    public Task<List<JobTitleDto>> TitlesAsync(CancellationToken ct) =>
        cache.GetOrSetAsync(CacheKeys.JobTitles, () => db.JobTitles.AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new JobTitleDto(t.Id, t.Name, db.Users.Count(u => u.JobTitleId == t.Id))).ToListAsync(ct), Ttl);

    public async Task<JobTitleDto> AddTitleAsync(SaveJobTitleRequest req, CancellationToken ct)
    {
        var name = req.Name.Trim();
        if (name.Length is < 2 or > 150) throw AppException.BadRequest("Unvan 2-150 karakter olmalıdır.");
        if (await db.JobTitles.AnyAsync(t => t.Name == name, ct)) throw AppException.Conflict("Bu unvan zaten tanımlı.");
        var t = new JobTitle { Name = name };
        db.JobTitles.Add(t);
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.JobTitles);
        return new JobTitleDto(t.Id, t.Name, 0);
    }

    public async Task DeleteTitleAsync(int id, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.JobTitleId == id, ct)) throw AppException.Conflict("Bu unvana sahip çalışanlar olduğu için silinemez.");
        await db.JobTitles.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
        await cache.RemoveAsync(CacheKeys.JobTitles);
    }

    public Task<List<GroupDto>> GroupsAsync(CancellationToken ct) =>
        db.Groups.AsNoTracking().OrderBy(g => g.Name).Select(g => new GroupDto(g.Id, g.Name, g.Description,
            g.Department == null ? null : new DeptRef(g.Department.Id, g.Department.Name), g.IsSupportGroup,
            g.Members.Select(m => new GroupMemberDto(m.UserId, m.User.FirstName + " " + m.User.LastName, m.User.JobTitle == null ? null : m.User.JobTitle.Name, m.IsLead)).ToList(),
            db.Tickets.Count(t => t.GroupId == g.Id && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed))).ToListAsync(ct);

    public async Task<GroupDto> SaveGroupAsync(int? id, SaveGroupRequest req, CancellationToken ct)
    {
        var name = req.Name.Trim();
        if (await db.Groups.AnyAsync(g => g.Name == name && g.Id != id, ct)) throw AppException.Conflict("Bu isimde bir grup zaten var.");
        Group g;
        if (id is { } existing) g = await db.Groups.Include(x => x.Members).FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw AppException.NotFound("Grup");
        else db.Groups.Add(g = new Group { Name = name });
        g.Name = name; g.Description = req.Description?.Trim(); g.DepartmentId = req.DepartmentId; g.IsSupportGroup = req.IsSupportGroup;
        if (req.MemberIds is { } members)
        {
            var valid = await db.Users.Where(u => members.Contains(u.Id) && u.IsActive).Select(u => u.Id).ToListAsync(ct);
            g.Members.Clear();
            foreach (var uid in valid) g.Members.Add(new GroupMember { UserId = uid, IsLead = req.LeadIds?.Contains(uid) == true });
        }
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.Groups);
        return (await GroupsAsync(ct)).First(x => x.Id == g.Id);
    }

    public async Task DeleteGroupAsync(int id, CancellationToken ct)
    {
        if (await db.Tickets.AnyAsync(t => t.GroupId == id && t.Status != TicketStatus.Closed, ct)) throw AppException.Conflict("Gruba atanmış açık talepler olduğu için silinemez.");
        await db.Groups.Where(g => g.Id == id).ExecuteDeleteAsync(ct);
    }

    public Task<List<RoleDto>> RolesAsync(CancellationToken ct) =>
        db.Roles.AsNoTracking().OrderBy(r => r.Id).Select(r => new RoleDto(r.Id, r.Code, r.Name, r.Description, r.IsSystem, r.Permissions, r.Users.Count)).ToListAsync(ct);

    public async Task<RoleDto> SaveRoleAsync(int? id, SaveRoleRequest req, CancellationToken ct)
    {
        Role r;
        if (id is { } existing)
        {
            r = await db.Roles.FirstOrDefaultAsync(x => x.Id == existing, ct) ?? throw AppException.NotFound("Rol");
            if (r.Code == RoleCodes.SuperAdmin) throw AppException.BadRequest("Super Admin rolünün yetkileri değiştirilemez.");
            if (r.IsSystem && r.Code != req.Code) throw AppException.BadRequest("Sistem rollerinin kodu değiştirilemez.");
        }
        else
        {
            if (await db.Roles.AnyAsync(x => x.Code == req.Code, ct)) throw AppException.Conflict("Bu rol kodu zaten var.");
            db.Roles.Add(r = new Role { Code = req.Code, Name = req.Name });
        }
        r.Code = req.Code; r.Name = req.Name.Trim(); r.Description = req.Description?.Trim(); r.Permissions = req.Permissions.Distinct().ToList();
        await db.SaveChangesAsync(ct);
        return (await RolesAsync(ct)).First(x => x.Id == r.Id);
    }

    public async Task DeleteRoleAsync(int id, CancellationToken ct)
    {
        var r = await db.Roles.Include(x => x.Users).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Rol");
        if (r.IsSystem) throw AppException.BadRequest("Sistem rolleri silinemez.");
        if (r.Users.Count > 0) throw AppException.Conflict("Bu role atanmış kullanıcılar var.");
        db.Roles.Remove(r);
        await db.SaveChangesAsync(ct);
    }
}

public class AccountRequestService(AppDbContext db, UserService users, IEventBus events, TimeProvider clock, ILogger<AccountRequestService> logger)
{
    public Task<List<AccountRequestDto>> ListAsync(UserContext me, AccountRequestStatus? status, CancellationToken ct)
    {
        var q = db.AccountRequests.AsNoTracking().AsQueryable();
        if (!me.Has(Permissions.AccountsApprove)) q = q.Where(r => r.RequestedById == me.Id);
        if (status is { } s) q = q.Where(r => r.Status == s);
        return q.OrderByDescending(r => r.CreatedAt).Select(r => new AccountRequestDto(r.Id, r.FirstName, r.LastName, r.Email, r.EmployeeNo,
            new DeptRef(r.Department.Id, r.Department.Name), r.JobTitle == null ? null : r.JobTitle.Name,
            r.Manager == null ? null : new Ref(r.Manager.Id, r.Manager.FirstName + " " + r.Manager.LastName), r.StartDate, r.EmploymentType, r.Phone, r.Note, r.Status,
            new Ref(r.RequestedBy.Id, r.RequestedBy.FirstName + " " + r.RequestedBy.LastName), r.CreatedAt,
            r.ProcessedBy == null ? null : new Ref(r.ProcessedBy.Id, r.ProcessedBy.FirstName + " " + r.ProcessedBy.LastName), r.ProcessedAt, r.RejectReason, r.CreatedUserId))
            .Take(200).ToListAsync(ct);
    }

    public async Task<int> CreateAsync(CreateAccountRequest req, UserContext me, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) throw AppException.Conflict("Bu e-posta adresiyle kayıtlı bir hesap zaten var.");
        if (await db.AccountRequests.AnyAsync(r => r.Email == email && r.Status == AccountRequestStatus.Pending, ct)) throw AppException.Conflict("Bu e-posta için bekleyen bir talep var.");
        var r = new AccountRequest
        {
            RequestedById = me.Id, FirstName = req.FirstName.Trim(), LastName = req.LastName.Trim(), Email = email, EmployeeNo = req.EmployeeNo?.Trim(),
            DepartmentId = req.DepartmentId, JobTitleId = req.JobTitleId, ManagerId = req.ManagerId, StartDate = req.StartDate, EmploymentType = req.EmploymentType,
            Phone = req.Phone?.Trim(), Note = req.Note?.Trim(), Status = AccountRequestStatus.Pending,
        };
        db.AccountRequests.Add(r);
        var approvers = await db.Users.Where(u => u.IsActive && u.Roles.Any(x => x.Role.Permissions.Contains(Permissions.AccountsApprove))).Select(u => u.Id).ToListAsync(ct);
        events.Enqueue(new AccountEvent("request_created", $"{r.FirstName} {r.LastName}", null, approvers) { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Hesap talebi oluşturuldu {RequestId}", r.Id);
        return r.Id;
    }

    public async Task<CreatedUserResponse> ApproveAsync(int id, UserContext me, CancellationToken ct)
    {
        var r = await db.AccountRequests.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Hesap talebi");
        if (r.Status != AccountRequestStatus.Pending) throw AppException.BadRequest("Talep daha önce işlenmiş.");
        var temp = PasswordPolicy.Generate();
        var user = await users.CreateFromRequestAsync(r, temp, me, ct);
        r.Status = AccountRequestStatus.Approved;
        r.ProcessedById = me.Id;
        r.ProcessedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        r.CreatedUserId = user.Id;
        events.Enqueue(new AccountEvent("account_created", user.FullName, user.Id, [r.RequestedById]) { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Hesap talebi onaylandı {RequestId} → kullanıcı {UserId}", id, user.Id);
        return new CreatedUserResponse(await users.GetAsync(user.Id, ct), temp);
    }

    public async Task RejectAsync(int id, RejectRequest req, UserContext me, CancellationToken ct)
    {
        var r = await db.AccountRequests.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Hesap talebi");
        if (r.Status != AccountRequestStatus.Pending) throw AppException.BadRequest("Talep daha önce işlenmiş.");
        if (string.IsNullOrWhiteSpace(req.Reason)) throw AppException.BadRequest("Ret gerekçesi zorunludur.");
        r.Status = AccountRequestStatus.Rejected;
        r.RejectReason = req.Reason.Trim();
        r.ProcessedById = me.Id;
        r.ProcessedAt = clock.GetUtcNow().UtcDateTime;
        events.Enqueue(new DirectNotificationEvent([r.RequestedById], "Hesap talebi reddedildi", $"{r.FirstName} {r.LastName} için hesap talebi reddedildi: {r.RejectReason}", "warning", "/admin/account-requests") { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
    }
}

public class DirectoryService(AppDbContext db, TimeProvider clock)
{
    public Task<PagedResult<DirectoryEntry>> SearchAsync(UserQuery q, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (q.DepartmentId is { } d) query = query.Where(u => u.DepartmentId == d);
        if (q.Ids is { Count: > 0 } ids) query = query.Where(u => ids.Contains(u.Id));
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var t = q.Search.Like();
            query = query.Where(u => EF.Functions.ILike(u.FirstName + " " + u.LastName, t) || EF.Functions.ILike(u.Email, t)
                                     || (u.JobTitle != null && EF.Functions.ILike(u.JobTitle.Name, t)) || (u.Department != null && EF.Functions.ILike(u.Department.Name, t)));
        }
        // Çalışan aramasında yalnızca iş iletişim bilgileri döner; doğum yılı, kimlik vb. özel veriler dönmez.
        return query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToPagedAsync(q, u => new DirectoryEntry(u.Id, u.FirstName + " " + u.LastName, u.Email, u.Phone,
            u.JobTitle == null ? null : u.JobTitle.Name, u.Department == null ? null : new DeptRef(u.Department.Id, u.Department.Name), u.Location,
            u.Manager == null ? null : new Ref(u.Manager.Id, u.Manager.FirstName + " " + u.Manager.LastName), u.HireDate), ct);
    }

    /// <summary>Tek çalışanın iş iletişim bilgileri (ör. form tasarımcısında seçili onaylayıcıyı göstermek için).</summary>
    public async Task<DirectoryEntry> GetAsync(Guid id, CancellationToken ct) =>
        (await SearchAsync(new UserQuery { Ids = [id], PageSize = 1 }, ct)).Items.FirstOrDefault() ?? throw AppException.NotFound("Çalışan");

    public async Task<List<BirthdayEntry>> BirthdaysAsync(int days, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var people = await db.Users.AsNoTracking().Where(u => u.IsActive && u.BirthDate != null)
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName, Title = u.JobTitle == null ? null : u.JobTitle.Name, Dept = u.Department == null ? null : u.Department.Name, u.BirthDate })
            .ToListAsync(ct);
        return people.Select(p =>
            {
                var b = p.BirthDate!.Value;
                var next = new DateOnly(today.Year, b.Month, Math.Min(b.Day, DateTime.DaysInMonth(today.Year, b.Month)));
                if (next < today) next = next.AddYears(1);
                return (p, next);
            })
            .Where(x => x.next.DayNumber - today.DayNumber <= days)
            .OrderBy(x => x.next)
            .Select(x => new BirthdayEntry(x.p.Id, x.p.Name, x.p.Title, x.p.Dept, x.next.Month, x.next.Day, x.next == today))
            .ToList();
    }
}

// ============================================================ Controller'lar
[Route("api/auth")]
public class AuthController(AuthService auth, IOptions<JwtOptions> jwt, IWebHostEnvironment env) : ApiController
{
    private const string Cookie = "sd_rt";

    private void SetCookie(string token) => Response.Cookies.Append(Cookie, token, new CookieOptions
    {
        HttpOnly = true, Secure = Request.IsHttps || !env.IsDevelopment(), SameSite = SameSiteMode.Strict, Path = "/api/auth",
        Expires = DateTimeOffset.UtcNow.AddDays(jwt.Value.RefreshTokenDays),
    });

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// 1. adım: e-posta ve parola. Doğrulama kodu açıksa <c>requiresCode=true</c> ve meydan okuma bilgisi döner (kod e-postayla gönderilir);
    /// kapalıysa oturum bilgisi döner ve refresh token httpOnly çerezle yazılır.
    /// </summary>
    [AllowAnonymous, EnableRateLimiting("public"), HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        var (res, refresh) = await auth.LoginAsync(req, Ip, ct);
        if (refresh is not null) SetCookie(refresh);
        return Ok(res, res.RequiresCode ? "Doğrulama kodu e-posta adresinize gönderildi." : null);
    }

    /// <summary>2. adım: e-postaya gelen doğrulama kodu. Başarılıysa oturum açılır.</summary>
    [AllowAnonymous, EnableRateLimiting("public"), HttpPost("login/verify")]
    public async Task<IActionResult> VerifyCode(VerifyLoginCodeRequest req, CancellationToken ct)
    {
        var (res, refresh) = await auth.VerifyLoginCodeAsync(req, Ip, ct);
        SetCookie(refresh);
        return Ok(res);
    }

    /// <summary>Yeni doğrulama kodu (bekleme süresi ve gönderim sınırı uygulanır).</summary>
    [AllowAnonymous, EnableRateLimiting("public"), HttpPost("login/resend")]
    public async Task<IActionResult> ResendCode(ResendLoginCodeRequest req, CancellationToken ct) =>
        Ok(await auth.ResendLoginCodeAsync(req.ChallengeId, ct), "Yeni doğrulama kodu gönderildi.");

    /// <summary>Refresh token ile yeni erişim token'ı (token rotasyonu uygulanır).</summary>
    [AllowAnonymous, HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var (res, refresh) = await auth.RefreshAsync(Request.Cookies[Cookie], Ip, ct);
        SetCookie(refresh);
        return Ok(res);
    }

    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[Cookie], ct);
        Response.Cookies.Delete(Cookie, new CookieOptions { Path = "/api/auth" });
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> MeInfo(CancellationToken ct) => Ok(await auth.MeAsync(Me.Id, ct));

    /// <summary>Parola değiştirme. İlk girişte zorunludur.</summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
    {
        var (res, refresh) = await auth.ChangePasswordAsync(Me, req, Ip, ct);
        SetCookie(refresh);
        return Ok(res, "Parolanız güncellendi.");
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest req, CancellationToken ct) => Ok(await auth.UpdateProfileAsync(Me, req, ct), "Profiliniz güncellendi.");
}

[Route("api/users")]
public class UsersController(UserService users) : ApiController
{
    [HasPermission(Permissions.UsersManage), HttpGet]
    public async Task<IActionResult> List([FromQuery] UserQuery q, CancellationToken ct) => Ok(await users.ListAsync(q, ct));

    [HasPermission(Permissions.UsersManage), HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await users.GetAsync(id, ct));

    /// <summary>Yeni hesap oluşturur; geçici parola yalnızca bu yanıtta bir kez gösterilir.</summary>
    [HasPermission(Permissions.UsersManage), HttpPost]
    public async Task<IActionResult> Create(SaveUserRequest req, CancellationToken ct) => Created(await users.CreateAsync(req, Me, ct), "Hesap oluşturuldu.");

    [HasPermission(Permissions.UsersManage), HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveUserRequest req, CancellationToken ct) => Ok(await users.UpdateAsync(id, req, ct), "Kullanıcı güncellendi.");

    [HasPermission(Permissions.UsersManage, Permissions.RolesManage), HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, SetRolesRequest req, CancellationToken ct) => Ok(await users.SetRolesAsync(id, req, Me, ct), "Roller güncellendi.");

    [HasPermission(Permissions.UsersManage), HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, DeactivateRequest req, CancellationToken ct) => Ok(await users.DeactivateAsync(id, req, Me, ct), "Hesap devre dışı bırakıldı.");

    [HasPermission(Permissions.UsersManage), HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => Ok(await users.ActivateAsync(id, ct), "Hesap etkinleştirildi.");

    [HasPermission(Permissions.UsersManage), HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken ct) => Ok(new { temporaryPassword = await users.ResetPasswordAsync(id, Me, ct) }, "Geçici parola oluşturuldu.");
}

[Route("api/directory")]
public class DirectoryController(DirectoryService directory) : ApiController
{
    /// <summary>Çalışan arama (tüm çalışanlar). Yalnızca iş iletişim bilgileri döner.</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] UserQuery q, CancellationToken ct) => Ok(await directory.SearchAsync(q, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await directory.GetAsync(id, ct));

    [HttpGet("birthdays")]
    public async Task<IActionResult> Birthdays([FromQuery] int days = 7, CancellationToken ct = default) => Ok(await directory.BirthdaysAsync(Math.Clamp(days, 0, 31), ct));
}

[Route("api/org")]
public class OrganizationController(OrganizationService org) : ApiController
{
    [HttpGet("departments")]
    public async Task<IActionResult> Departments(CancellationToken ct) => Ok(await org.DepartmentsAsync(ct));

    [HasPermission(Permissions.OrgManage), HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment(SaveDepartmentRequest req, CancellationToken ct) => Created(await org.SaveDepartmentAsync(null, req, ct), "Departman eklendi.");

    [HasPermission(Permissions.OrgManage), HttpPut("departments/{id:int}")]
    public async Task<IActionResult> UpdateDepartment(int id, SaveDepartmentRequest req, CancellationToken ct) => Ok(await org.SaveDepartmentAsync(id, req, ct), "Departman güncellendi.");

    [HttpGet("titles")]
    public async Task<IActionResult> Titles(CancellationToken ct) => Ok(await org.TitlesAsync(ct));

    [HasPermission(Permissions.OrgManage), HttpPost("titles")]
    public async Task<IActionResult> AddTitle(SaveJobTitleRequest req, CancellationToken ct) => Created(await org.AddTitleAsync(req, ct), "Unvan eklendi.");

    [HasPermission(Permissions.OrgManage), HttpDelete("titles/{id:int}")]
    public async Task<IActionResult> DeleteTitle(int id, CancellationToken ct) { await org.DeleteTitleAsync(id, ct); return NoContent(); }

    [HttpGet("groups")]
    public async Task<IActionResult> Groups(CancellationToken ct) => Ok(await org.GroupsAsync(ct));

    [HasPermission(Permissions.OrgManage, Permissions.TicketsConfigure), HttpPost("groups")]
    public async Task<IActionResult> CreateGroup(SaveGroupRequest req, CancellationToken ct) => Created(await org.SaveGroupAsync(null, req, ct), "Grup oluşturuldu.");

    [HasPermission(Permissions.OrgManage, Permissions.TicketsConfigure), HttpPut("groups/{id:int}")]
    public async Task<IActionResult> UpdateGroup(int id, SaveGroupRequest req, CancellationToken ct) => Ok(await org.SaveGroupAsync(id, req, ct), "Grup güncellendi.");

    [HasPermission(Permissions.OrgManage, Permissions.TicketsConfigure), HttpDelete("groups/{id:int}")]
    public async Task<IActionResult> DeleteGroup(int id, CancellationToken ct) { await org.DeleteGroupAsync(id, ct); return NoContent(); }

    [HasPermission(Permissions.UsersManage, Permissions.RolesManage), HttpGet("roles")]
    public async Task<IActionResult> Roles(CancellationToken ct) => Ok(await org.RolesAsync(ct));

    [HasPermission(Permissions.UsersManage, Permissions.RolesManage), HttpGet("permissions")]
    public IActionResult PermissionCatalog() => Ok(Permissions.All.Select(p => new PermissionDto(p.Code, p.Group, p.Label)).ToList());

    [HasPermission(Permissions.RolesManage), HttpPost("roles")]
    public async Task<IActionResult> CreateRole(SaveRoleRequest req, CancellationToken ct) => Created(await org.SaveRoleAsync(null, req, ct), "Rol oluşturuldu.");

    [HasPermission(Permissions.RolesManage), HttpPut("roles/{id:int}")]
    public async Task<IActionResult> UpdateRole(int id, SaveRoleRequest req, CancellationToken ct) => Ok(await org.SaveRoleAsync(id, req, ct), "Rol güncellendi.");

    [HasPermission(Permissions.RolesManage), HttpDelete("roles/{id:int}")]
    public async Task<IActionResult> DeleteRole(int id, CancellationToken ct) { await org.DeleteRoleAsync(id, ct); return NoContent(); }
}

[Route("api/account-requests")]
public class AccountRequestsController(AccountRequestService service) : ApiController
{
    [HasPermission(Permissions.AccountsRequest, Permissions.AccountsApprove), HttpGet]
    public async Task<IActionResult> List([FromQuery] AccountRequestStatus? status, CancellationToken ct) => Ok(await service.ListAsync(Me, status, ct));

    /// <summary>İK, yeni personel için hesap açma talebi gönderir.</summary>
    [HasPermission(Permissions.AccountsRequest), HttpPost]
    public async Task<IActionResult> Create(CreateAccountRequest req, CancellationToken ct) => Created(new { id = await service.CreateAsync(req, Me, ct) }, "Hesap talebi IT ekibine iletildi.");

    /// <summary>IT/Admin talebi onaylar ve hesabı oluşturur.</summary>
    [HasPermission(Permissions.AccountsApprove), HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, CancellationToken ct) => Ok(await service.ApproveAsync(id, Me, ct), "Hesap oluşturuldu.");

    [HasPermission(Permissions.AccountsApprove), HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, RejectRequest req, CancellationToken ct) { await service.RejectAsync(id, req, Me, ct); return NoContent(); }
}

[Route("api/audit-logs")]
public class AuditController(AppDbContext db) : ApiController
{
    [HasPermission(Permissions.AuditView), HttpGet]
    public async Task<IActionResult> List([FromQuery] AuditQuery q, CancellationToken ct)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.EntityType)) query = query.Where(a => a.EntityType == q.EntityType);
        if (!string.IsNullOrWhiteSpace(q.Action)) query = query.Where(a => a.Action == q.Action);
        if (q.UserId is { } u) query = query.Where(a => a.UserId == u);
        if (q.From is { } f) query = query.Where(a => a.CreatedAt >= f.ToUniversalTime());
        if (q.To is { } t) query = query.Where(a => a.CreatedAt <= t.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(a => (a.UserName != null && EF.Functions.ILike(a.UserName, q.Search.Like())) || a.EntityId == q.Search);
        return Ok(await query.OrderByDescending(a => a.Id).ToPagedAsync(q, a => new AuditLogDto(a.Id, a.UserId, a.UserName, a.Action, a.EntityType, a.EntityId, a.Changes, a.IpAddress, a.CreatedAt), ct));
    }

    [HasPermission(Permissions.AuditView), HttpGet("entity-types")]
    public async Task<IActionResult> EntityTypes(CancellationToken ct) => Ok(await db.AuditLogs.Select(a => a.EntityType).Distinct().OrderBy(x => x).ToListAsync(ct));
}
