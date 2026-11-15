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
