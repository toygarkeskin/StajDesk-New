using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Common;

// ======================= JWT =======================

public class JwtOptions
{
    public string Issuer { get; set; } = "stajdesk";
    public string Audience { get; set; } = "stajdesk-web";
    public string Key { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}

public class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var o = options.Value;
        var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(o.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("name", user.FullName),
            new("email", user.Email),
        };
        if (user.DepartmentId is { } d) claims.Add(new Claim("dept", d.ToString()));
        if (user.MustChangePassword) claims.Add(new Claim("mcp", "1"));
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));

        // İçerik base64 ile okunabilir: yalnızca kimlik, rol ve izin kodları taşınır; parola veya kişisel veri asla.
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Issuer, Audience = o.Audience, Expires = expires, Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }

    /// <summary>Kriptografik rastgele refresh token üretir; veritabanında yalnızca SHA-256 özeti saklanır.</summary>
    public static (string Raw, string Hash) CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (raw, HashToken(raw));
    }

    public static string HashToken(string raw) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}

// ======================= Redis önbellek =======================

public interface ICacheService
{
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl) => GetOrSetAsync(key, factory, _ => ttl);
    /// <summary>Süre, üretilen değere göre seçilir (ör. dış kaynak hatası içeren sonuç kısa süre önbelleğe alınır).</summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, Func<T, TimeSpan> ttl);
    Task RemoveAsync(params string[] keys);
    Task<long> RemoveByPrefixAsync(string prefix);
    Task<IReadOnlyList<(string Key, TimeSpan? Ttl)>> ListAsync(string prefix);
}

public static class CacheKeys
{
    public const string Prefix = "stajdesk:";
    public const string Categories = "stajdesk:lookup:categories";
    public const string Departments = "stajdesk:lookup:departments";
    public const string JobTitles = "stajdesk:lookup:titles";
    public const string Groups = "stajdesk:lookup:groups";
    public const string Forms = "stajdesk:lookup:forms";
    public const string PublicHome = "stajdesk:public:home";
    public const string Market = "stajdesk:public:market";
    public const string Weather = "stajdesk:public:weather";
    public const string StatsPrefix = "stajdesk:stats:";
    public static string Dashboard(Guid id) => $"stajdesk:stats:dashboard:{id}";
}

/// <summary>
/// Cache-aside: önce Redis'e bakılır, yoksa kaynak okunup Redis'e yazılır.
/// Redis erişilemezse uygulama durmaz, doğrudan veritabanıyla devam eder.
/// </summary>
public class RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions Json = MessagingTopology.Json;

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, Func<T, TimeSpan> ttl)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var cached = await redis.GetDatabase().StringGetAsync(key);
            if (cached.HasValue)
            {
                logger.LogDebug("Önbellek isabeti {Key} {Elapsed} ms", key, sw.ElapsedMilliseconds);
                return JsonSerializer.Deserialize<T>(cached.ToString(), Json)!;
            }
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Redis okunamadı, kaynağa düşülüyor: {Error}", ex.Message);
            return await factory();
        }

        var value = await factory();
        logger.LogDebug("Önbellek ıskası {Key}, kaynak {Elapsed} ms", key, sw.ElapsedMilliseconds);
        try { await redis.GetDatabase().StringSetAsync(key, JsonSerializer.Serialize(value, Json), ttl(value)); }
        catch (Exception ex) when (ex is RedisException or TimeoutException) { logger.LogWarning("Redis'e yazılamadı: {Error}", ex.Message); }
        return value;
    }

    public async Task RemoveAsync(params string[] keys)
    {
        try { await redis.GetDatabase().KeyDeleteAsync(keys.Select(k => (RedisKey)k).ToArray()); }
        catch (Exception ex) when (ex is RedisException or TimeoutException) { logger.LogWarning("Önbellek temizlenemedi: {Error}", ex.Message); }
    }

    public async Task<long> RemoveByPrefixAsync(string prefix)
    {
        long removed = 0;
        foreach (var ep in redis.GetEndPoints())
        {
            var server = redis.GetServer(ep);
            var keys = server.Keys(pattern: prefix + "*", pageSize: 500).ToArray();
            if (keys.Length > 0) removed += await redis.GetDatabase().KeyDeleteAsync(keys);
        }
        return removed;
    }

    public async Task<IReadOnlyList<(string Key, TimeSpan? Ttl)>> ListAsync(string prefix)
    {
        var list = new List<(string, TimeSpan?)>();
        foreach (var ep in redis.GetEndPoints())
            foreach (var key in redis.GetServer(ep).Keys(pattern: prefix + "*", pageSize: 500).Take(500))
                list.Add((key.ToString(), await redis.GetDatabase().KeyTimeToLiveAsync(key)));
        return list.OrderBy(k => k.Item1).ToList();
    }
}

/// <summary>Aynı hesap için 5 dakikada en fazla 5 hatalı giriş (Redis INCR + EXPIRE). Redis yoksa koruma geçici olarak devre dışı kalır.</summary>
public class LoginRateLimiter(IConnectionMultiplexer redis, ILogger<LoginRateLimiter> logger)
{
    public const int MaxAttempts = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);
    private static string Key(string email) => $"stajdesk:ratelimit:login:{email.Trim().ToLowerInvariant()}";

    public async Task<int?> GetLockSecondsAsync(string email)
    {
        try
        {
            var db = redis.GetDatabase();
            var count = (int?)await db.StringGetAsync(Key(email)) ?? 0;
            if (count < MaxAttempts) return null;
            var ttl = await db.KeyTimeToLiveAsync(Key(email));
            return (int)Math.Ceiling((ttl ?? Window).TotalSeconds);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Giriş hız sınırlaması devre dışı (Redis erişilemiyor): {Error}", ex.Message);
            return null;
        }
    }

    public async Task<long> RegisterFailureAsync(string email)
    {
        try
        {
            var db = redis.GetDatabase();
            var count = await db.StringIncrementAsync(Key(email));
            if (count == 1) await db.KeyExpireAsync(Key(email), Window);
            return count;
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning("Hatalı giriş sayacı yazılamadı: {Error}", ex.Message);
            return 0;
        }
    }

    public async Task ResetAsync(string email)
    {
        try { await redis.GetDatabase().KeyDeleteAsync(Key(email)); }
        catch (Exception ex) when (ex is RedisException or TimeoutException) { logger.LogDebug("{Error}", ex.Message); }
    }
}

// ======================= Dosya depolama =======================

public class FileStorageOptions
{
    public string Root { get; set; } = "data/uploads";
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;
}

/// <summary>Yüklenen dosyaları uzantı, MIME türü, imza (magic number) ve boyuta göre doğrular; diske rastgele adla yazar.</summary>
public class FileStorage(IOptions<FileStorageOptions> options, AppDbContext db, ILogger<FileStorage> logger)
{
    private static readonly Dictionary<string, (string Mime, byte[][] Signatures)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ("application/pdf", [[0x25, 0x50, 0x44, 0x46]]),
        [".png"] = ("image/png", [[0x89, 0x50, 0x4E, 0x47]]),
        [".jpg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".jpeg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        [".webp"] = ("image/webp", [[0x52, 0x49, 0x46, 0x46]]),
        [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document", [[0x50, 0x4B, 0x03, 0x04]]),
        [".xlsx"] = ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [[0x50, 0x4B, 0x03, 0x04]]),
        [".csv"] = ("text/csv", []),
        [".txt"] = ("text/plain", []),
    };

    public string Root => Path.GetFullPath(options.Value.Root);

    public async Task<StoredFile> SaveAsync(IFormFile file, Guid uploaderId, CancellationToken ct)
    {
        if (file.Length == 0) throw AppException.BadRequest("Boş dosya yüklenemez.");
        if (file.Length > options.Value.MaxBytes)
            throw AppException.BadRequest($"Dosya boyutu en fazla {options.Value.MaxBytes / 1024 / 1024} MB olabilir.");
        var ext = Path.GetExtension(file.FileName);
        if (!Allowed.TryGetValue(ext, out var rule))
            throw AppException.BadRequest("Bu dosya türü desteklenmiyor. İzin verilen türler: PDF, PNG, JPG, WEBP, DOCX, XLSX, CSV, TXT.");

        await using var input = file.OpenReadStream();
        var head = new byte[8];
        var read = await input.ReadAsync(head, ct);
        if (rule.Signatures.Length > 0 && !rule.Signatures.Any(sig => read >= sig.Length && head.AsSpan(0, sig.Length).SequenceEqual(sig)))
            throw AppException.BadRequest("Dosya içeriği uzantısıyla uyuşmuyor.");
        input.Position = 0;

        var id = Guid.NewGuid();
        var rel = Path.Combine(DateTime.UtcNow.ToString("yyyy-MM"), id.ToString("N") + ext.ToLowerInvariant());
        var full = Path.Combine(Root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using (var output = File.Create(full)) await input.CopyToAsync(output, ct);

        var safeName = Path.GetFileName(file.FileName);
        if (safeName.Length > 200) safeName = safeName[^200..];
        var stored = new StoredFile { Id = id, FileName = safeName, ContentType = rule.Mime, Size = file.Length, StoragePath = rel, UploadedById = uploaderId };
        db.Files.Add(stored);
        logger.LogInformation("Dosya yüklendi {FileId} {Size} bayt", id, file.Length);
        return stored;
    }

    public Stream Open(StoredFile file)
    {
        var full = Path.GetFullPath(Path.Combine(Root, file.StoragePath));
        if (!full.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) throw AppException.Forbidden();
        if (!File.Exists(full)) throw AppException.NotFound("Dosya");
        return File.OpenRead(full);
    }
}

/// <summary>Redis erişilemezse "Degraded" döner: API önbelleksiz çalışmaya devam edebildiği için hazır sayılır.</summary>
public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var ping = await redis.GetDatabase().PingAsync();
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy($"{ping.TotalMilliseconds:0.0} ms");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(ex.Message);
        }
    }
}

// ======================= Arka plan servisleri =======================

/// <summary>Outbox'taki olayları 2 saniyede bir RabbitMQ'ya aktarır.</summary>
public class OutboxPublisherService(IServiceScopeFactory scopes, ILogger<OutboxPublisherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                var sent = await dispatcher.DispatchAsync(50, stoppingToken);
                if (sent == 50) continue; // biriken varsa beklemeden devam et
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Outbox aktarımı başarısız: {Error}", ex.Message);
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
