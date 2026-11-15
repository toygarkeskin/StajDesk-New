using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public class SystemService(AppDbContext db, IConnectionMultiplexer redis, RabbitMqPublisher rabbit, ICacheService cache, IHttpClientFactory http, IConfiguration config)
{
    public async Task<SystemHealth> HealthAsync(CancellationToken ct)
    {
        var dbCheck = await Measure("PostgreSQL", "Kalıcı veri (EF Core)", async () =>
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
            return $"{await db.Users.CountAsync(ct)} kullanıcı, {await db.Tickets.CountAsync(ct)} talep";
        });
        var others = await Task.WhenAll(
            Measure("Redis", "Önbellek ve hız sınırlama", async () => $"PING {(await redis.GetDatabase().PingAsync()).TotalMilliseconds:0.0} ms"),
            Measure("RabbitMQ", "Olay kuyruğu", async () =>
            {
                var (messages, consumers) = await rabbit.InspectAsync(MessagingTopology.NotificationQueue, ct);
                return $"Kuyrukta {messages} mesaj · {consumers} tüketici";
            }),
            Measure("Worker", "Bildirim servisi", async () =>
            {
                var (_, consumers) = await rabbit.InspectAsync(MessagingTopology.NotificationQueue, ct);
                if (consumers == 0) throw new InvalidOperationException("Kuyruğu dinleyen Worker yok; mesajlar kuyrukta bekliyor.");
                return $"{consumers} aktif tüketici";
            }),
            Measure("Seq", "Merkezi loglama", async () =>
            {
                using var res = await http.CreateClient("external").GetAsync($"{(config["Seq:ServerUrl"] ?? "http://localhost:5341").TrimEnd('/')}/health", ct);
                res.EnsureSuccessStatusCode();
                return "Log toplama aktif";
            }));
        var pending = await db.Outbox.CountAsync(m => m.PublishedAt == null && m.Attempts < 20, ct);
        var failed = await db.Outbox.CountAsync(m => m.PublishedAt == null && m.Attempts >= 20, ct);
        var all = new[] { dbCheck }.Concat(others).ToList();
        return new SystemHealth(all.All(c => c.Healthy), DateTime.UtcNow, all, pending, failed);
    }

    public async Task<List<CacheEntry>> CacheAsync() =>
        (await cache.ListAsync(CacheKeys.Prefix)).Select(k => new CacheEntry(k.Key, k.Ttl?.TotalSeconds)).ToList();

    public Task<long> ClearCacheAsync(string? prefix)
    {
        var p = string.IsNullOrWhiteSpace(prefix) ? CacheKeys.Prefix : prefix;
        if (!p.StartsWith(CacheKeys.Prefix)) throw AppException.BadRequest("Yalnızca StajDesk önbellek anahtarları temizlenebilir.");
        if (p.StartsWith("stajdesk:ratelimit")) throw AppException.BadRequest("Güvenlik nedeniyle hız sınırlama sayaçları buradan temizlenemez.");
        return p == CacheKeys.Prefix ? ClearAllExceptRateLimitAsync() : cache.RemoveByPrefixAsync(p);
    }

    private async Task<long> ClearAllExceptRateLimitAsync()
    {
        long total = 0;
        foreach (var prefix in new[] { "stajdesk:lookup:", "stajdesk:stats:", "stajdesk:public:" }) total += await cache.RemoveByPrefixAsync(prefix);
        return total;
    }

    private static async Task<ComponentHealth> Measure(string name, string role, Func<Task<string>> probe)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var detail = await probe().WaitAsync(TimeSpan.FromSeconds(3));
            return new ComponentHealth(name, role, true, sw.ElapsedMilliseconds, detail);
        }
        catch (Exception ex)
        {
            return new ComponentHealth(name, role, false, sw.ElapsedMilliseconds, ex.Message);
        }
    }
}
