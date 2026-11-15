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
