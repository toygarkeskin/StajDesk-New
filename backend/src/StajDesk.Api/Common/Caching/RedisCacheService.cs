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
