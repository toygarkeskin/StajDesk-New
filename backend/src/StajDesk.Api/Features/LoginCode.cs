using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Email;

namespace StajDesk.Api.Features;

public sealed class LoginCodeOptions
{
    /// <summary>Girişte e-posta doğrulama kodu istensin mi? SMTP tanımlı değilse uygulanamaz ve kapalı kabul edilir.</summary>
    public bool Enabled { get; set; } = true;
    public int CodeLength { get; set; } = 6;
    public int ValidMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    public int MaxSends { get; set; } = 4;
    public int ResendCooldownSeconds { get; set; } = 30;
    /// <summary>Yalnızca geliştirme: giriş ekranında gösterilecek test posta kutusu adresi (ör. Mailpit). Kod asla API yanıtında dönmez.</summary>
    public string? DevMailboxUrl { get; set; }
}

/// <summary>Parola doğrulandıktan sonra dönen ikinci adım bilgisi. Kodun kendisi hiçbir yanıtta yer almaz.</summary>
public record LoginChallengeDto(Guid ChallengeId, string MaskedEmail, DateTime ExpiresAt, int ResendAfterSeconds, int CodeLength, string? DevMailboxUrl);
/// <summary>Giriş sonucu: ya doğrudan oturum (kod kapalıysa) ya da doğrulama adımı.</summary>
public record LoginResult(bool RequiresCode, LoginChallengeDto? Challenge, AuthResponse? Auth);
public record VerifyLoginCodeRequest(Guid ChallengeId, string Code);
public record ResendLoginCodeRequest(Guid ChallengeId);

public class VerifyLoginCodeValidator : AbstractValidator<VerifyLoginCodeRequest>
{
    public VerifyLoginCodeValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().WithMessage("Doğrulama kodunu giriniz.")
            .Matches("^[0-9]{4,8}$").WithMessage("Doğrulama kodu yalnızca rakamlardan oluşmalıdır.");
    }
}

/// <summary>
/// E-posta ile giriş doğrulama kodu. Kod kriptografik rastgele üretilir, yalnızca SHA-256 özeti (meydan okuma kimliğiyle tuzlanmış) saklanır,
/// sabit zamanlı karşılaştırılır. Süre, deneme ve yeniden gönderme sınırlıdır; başarılı doğrulamada tek kullanımlık olarak tüketilir.
/// Kod ve e-posta adresi loglanmaz.
/// </summary>
public class LoginCodeService(AppDbContext db, IEmailSender email, IOptions<LoginCodeOptions> options, TimeProvider clock, ILogger<LoginCodeService> logger)
{
    private LoginCodeOptions O => options.Value;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Kod gerçekten uygulanabilir mi (açık ve e-posta gönderilebiliyor)?</summary>
    public bool IsActive => O.Enabled && email.Enabled;

    public static string Hash(Guid challengeId, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{challengeId:N}:{code}")));

    public static string MaskEmail(string address)
    {
        var at = address.IndexOf('@');
        if (at <= 0) return "***";
        var local = address[..at];
        var visible = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visible}{new string('*', Math.Max(3, local.Length - visible.Length))}{address[at..]}";
    }

    private string NewCode() => RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, O.CodeLength)).ToString().PadLeft(O.CodeLength, '0');

    public async Task<LoginChallengeDto> CreateAsync(User user, string? ip, CancellationToken ct)
    {
        // Aynı kullanıcının önceki tüketilmemiş kodları geçersiz kılınır (yalnızca son kod geçerlidir)
        var pending = await db.LoginChallenges.Where(c => c.UserId == user.Id && c.ConsumedAt == null && c.ExpiresAt > Now).ToListAsync(ct);
        foreach (var p in pending) p.ExpiresAt = Now;
        var challenge = new LoginChallenge { UserId = user.Id, CodeHash = "", CreatedAt = Now, LastSentAt = Now, ExpiresAt = Now.AddMinutes(O.ValidMinutes), Ip = ip };
        var code = NewCode();
        challenge.CodeHash = Hash(challenge.Id, code);
        db.LoginChallenges.Add(challenge);
        await db.SaveChangesAsync(ct);
        await SendAsync(user, code, ct);
        logger.LogInformation("Giriş doğrulama kodu gönderildi {UserId}", user.Id);
        return Dto(challenge, user);
    }

    public async Task<LoginChallengeDto> ResendAsync(Guid challengeId, CancellationToken ct)
    {
        var c = await db.LoginChallenges.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == challengeId, ct);
        if (c is null || c.ConsumedAt is not null || c.ExpiresAt <= Now || !c.User.IsActive)
            throw new AppException(HttpStatusCode.BadRequest, "Doğrulama süresi doldu. Lütfen yeniden giriş yapınız.", "CODE_EXPIRED");
        var wait = O.ResendCooldownSeconds - (int)(Now - c.LastSentAt).TotalSeconds;
        if (wait > 0)
            throw new AppException(HttpStatusCode.TooManyRequests, $"Yeni kod için {wait} saniye bekleyiniz.", "RESEND_COOLDOWN") { RetryAfterSeconds = wait };
        if (c.SendCount >= O.MaxSends)
            throw new AppException(HttpStatusCode.TooManyRequests, "Çok fazla kod istendi. Lütfen yeniden giriş yapınız.", "TOO_MANY_CODES");

        var code = NewCode();
        c.CodeHash = Hash(c.Id, code);
        c.SendCount++;
        c.Attempts = 0;
        c.LastSentAt = Now;
        c.ExpiresAt = Now.AddMinutes(O.ValidMinutes);
        await db.SaveChangesAsync(ct);
        await SendAsync(c.User, code, ct);
        logger.LogInformation("Giriş doğrulama kodu yeniden gönderildi {UserId} ({Count}. gönderim)", c.UserId, c.SendCount);
        return Dto(c, c.User);
    }

    /// <summary>Kodu doğrular; başarılıysa kodu tüketir ve kullanıcıyı döner.</summary>
    public async Task<User> VerifyAsync(VerifyLoginCodeRequest req, CancellationToken ct)
    {
        var c = await db.LoginChallenges.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == req.ChallengeId, ct);
        if (c is null || c.ConsumedAt is not null || c.ExpiresAt <= Now)
            throw new AppException(HttpStatusCode.BadRequest, "Doğrulama kodunun süresi doldu. Lütfen yeniden giriş yapınız.", "CODE_EXPIRED");
        if (c.Attempts >= O.MaxAttempts)
            throw new AppException(HttpStatusCode.TooManyRequests, "Çok fazla hatalı deneme yapıldı. Lütfen yeniden giriş yapınız.", "TOO_MANY_ATTEMPTS");

        var ok = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(c.Id, req.Code.Trim())), Encoding.ASCII.GetBytes(c.CodeHash));
        if (!ok)
        {
            c.Attempts++;
            var left = O.MaxAttempts - c.Attempts;
            if (left <= 0) c.ExpiresAt = Now; // deneme hakkı bitti: kod geçersiz
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Hatalı doğrulama kodu {UserId} ({Attempt}/{Max})", c.UserId, c.Attempts, O.MaxAttempts);
            throw left <= 0
                ? new AppException(HttpStatusCode.TooManyRequests, "Çok fazla hatalı deneme yapıldı. Lütfen yeniden giriş yapınız.", "TOO_MANY_ATTEMPTS")
                : new ValidationAppException(new() { ["code"] = [$"Doğrulama kodu hatalı. Kalan deneme hakkı: {left}."] });
        }
        if (!c.User.IsActive)
            throw new AppException(HttpStatusCode.Forbidden, "Hesabınız devre dışı bırakılmıştır. Lütfen İnsan Kaynakları ile iletişime geçiniz.", "ACCOUNT_DISABLED");

        c.ConsumedAt = Now;
        await db.SaveChangesAsync(ct);
        return c.User;
    }

    private LoginChallengeDto Dto(LoginChallenge c, User user) =>
        new(c.Id, MaskEmail(user.Email), c.ExpiresAt, Math.Max(0, O.ResendCooldownSeconds - (int)(Now - c.LastSentAt).TotalSeconds), O.CodeLength, O.DevMailboxUrl);

    private async Task SendAsync(User user, string code, CancellationToken ct)
    {
        try { await SendCoreAsync(user, code, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError("Doğrulama kodu e-postası gönderilemedi {UserId}: {Error}", user.Id, ex.Message);
            throw new AppException(HttpStatusCode.ServiceUnavailable, "Doğrulama kodu şu anda gönderilemiyor. Lütfen biraz sonra tekrar deneyiniz.", "EMAIL_UNAVAILABLE");
        }
    }

    private Task SendCoreAsync(User user, string code, CancellationToken ct)
    {
        var message = $"StajDesk'e giriş için doğrulama kodunuz aşağıdadır. Kod {O.ValidMinutes} dakika geçerlidir. Bu girişi siz yapmadıysanız parolanızı değiştirip BT ile iletişime geçiniz.";
        return email.SendAsync([new EmailMessage(user.FullName, user.Email, "[StajDesk] Giriş doğrulama kodunuz",
            $"Merhaba {user.FirstName},\n\nGiriş doğrulama kodunuz: {code}\n\n{message}",
            EmailTemplate.Render(user.FirstName, "Giriş doğrulama kodu", message, null, code))], ct);
    }
}
