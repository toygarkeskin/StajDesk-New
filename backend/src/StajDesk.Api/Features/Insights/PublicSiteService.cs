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

public class PublicSiteService(AppDbContext db, CollaborationService collab, ICacheService cache, IHttpClientFactory http, TimeProvider clock, ILogger<PublicSiteService> logger)
{
    private static readonly Dictionary<string, (string Name, double Lat, double Lon)> Cities = new(StringComparer.OrdinalIgnoreCase)
    {
        ["istanbul"] = ("İstanbul", 41.0082, 28.9784), ["ankara"] = ("Ankara", 39.9334, 32.8597), ["izmir"] = ("İzmir", 38.4237, 27.1428), ["adana"] = ("Adana", 37.0000, 35.3213),
    };

    /// <summary>Genel tanıtım sitesi verisi. Yalnızca herkese açık işaretlenmiş içerik ve toplu istatistikler döner; çalışan kişisel bilgisi içermez.</summary>
    public Task<PublicHome> HomeAsync(CancellationToken ct) => cache.GetOrSetAsync(CacheKeys.PublicHome, async () =>
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var banners = (await collab.BannersAsync(ct)).Where(b => b.IsLive).ToList();
        var news = await db.Announcements.AsNoTracking().Where(a => a.IsPublic && a.PublishAt <= now && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.PublishAt).Take(6).Select(a => new AnnouncementDto(a.Id, a.Title, a.Summary, a.Body, a.Kind, true, a.IsPinned, a.PublishAt, a.ExpiresAt, null,
                new UserRef(Guid.Empty, "Kurumsal İletişim", ""))).ToListAsync(ct);
        var jobs = await collab.JobsAsync(true, true, ct);
        var events = await db.Events.AsNoTracking().Where(e => e.IsPublic && e.StartsAt >= now.AddHours(-3)).OrderBy(e => e.StartsAt).Take(4)
            .Select(e => new EventDto(e.Id, e.Title, e.Description, e.Location, e.Category, e.StartsAt, e.EndsAt, true, 0, false, new UserRef(Guid.Empty, "", ""))).ToListAsync(ct);
        var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var stats = new PublicStats(await db.Users.CountAsync(u => u.IsActive, ct), await db.Stores.CountAsync(s => s.Status != StoreStatus.Closed, ct),
            await db.Stores.Where(s => s.City != null).Select(s => s.City).Distinct().CountAsync(ct),
            await db.Departments.CountAsync(d => d.Type == DepartmentType.HeadOffice && d.IsActive, ct),
            await db.Tickets.CountAsync(t => t.ResolvedAt >= yearStart, ct) + await db.FormSubmissions.CountAsync(s => s.Status == SubmissionStatus.Completed && s.CompletedAt >= yearStart, ct));
        return new PublicHome(banners, news, jobs, events, stats);
    }, TimeSpan.FromMinutes(2));

    /// <summary>USD/EUR: TCMB gösterge kurları (döviz satış). Gram altın: Truncgil Finans piyasa verisi; erişilemezse gold-api.com XAU/USD × TCMB USD / 31,1035.</summary>
    public Task<MarketData> MarketAsync(CancellationToken ct) => cache.GetOrSetAsync(CacheKeys.Market, async () =>
    {
        var rates = new List<Rate>();
        var errors = new List<string>();
        var client = http.CreateClient("external");
        decimal? usd = null;
        try
        {
            var xml = XDocument.Parse(await client.GetStringAsync("https://www.tcmb.gov.tr/kurlar/today.xml", ct));
            var date = xml.Root?.Attribute("Tarih")?.Value ?? "";
            foreach (var (code, name) in new[] { ("USD", "ABD Doları"), ("EUR", "Euro") })
            {
                var node = xml.Root?.Elements("Currency").FirstOrDefault(e => e.Attribute("Kod")?.Value == code);
                if (decimal.TryParse(node?.Element("ForexSelling")?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                {
                    rates.Add(new Rate(code, name, v, "TCMB gösterge kuru (döviz satış)", date));
                    if (code == "USD") usd = v;
                }
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("TCMB kurları alınamadı: {Error}", ex.Message);
            errors.Add("Döviz kurları şu anda alınamıyor.");
        }
        // Gram altın: önce doğrudan piyasa verisi (Truncgil Finans, gram altın satış). Erişilemezse
        // gold-api.com ons fiyatı × TCMB USD ile hesaplanır. İkisi de yoksa değer gösterilmez.
        try
        {
            using var doc = JsonDocument.Parse(await client.GetStringAsync("https://finans.truncgil.com/today.json", ct));
            var raw = doc.RootElement.GetProperty("gram-altin").GetProperty("Satış").GetString() ?? "";
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out var price) || price <= 0)
                throw new FormatException("Gram altın fiyatı okunamadı.");
            var updated = doc.RootElement.TryGetProperty("Update_Date", out var u) ? u.GetString() ?? "" : "";
            rates.Add(new Rate("GRAM", "Gram Altın", price, "Truncgil Finans (gram altın satış)", updated));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Gram altın (Truncgil) alınamadı, hesaplanan değere geçiliyor: {Error}", ex.Message);
            try
            {
                using var doc = JsonDocument.Parse(await client.GetStringAsync("https://api.gold-api.com/price/XAU", ct));
                var ounceUsd = doc.RootElement.GetProperty("price").GetDecimal();
                var updated = doc.RootElement.TryGetProperty("updatedAt", out var u) ? u.GetString() ?? "" : "";
                if (usd is { } usdTry)
                    rates.Add(new Rate("GRAM", "Gram Altın", Math.Round(ounceUsd * usdTry / 31.1034768m, 2), "gold-api.com XAU/USD × TCMB USD (hesaplanan)", updated));
                else errors.Add("Gram altın hesaplanamadı (USD kuru alınamadı).");
            }
            catch (Exception ex2) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Altın fiyatı alınamadı: {Error}", ex2.Message);
                errors.Add("Altın fiyatı şu anda alınamıyor.");
            }
        }
        return new MarketData(rates, clock.GetUtcNow().UtcDateTime, errors);
    }, m => m.Errors.Count > 0 ? TimeSpan.FromMinutes(1) : TimeSpan.FromMinutes(15)); // kaynak hatası 15 dk boyunca önbellekte kalmasın


    public async Task<WeatherData?> WeatherAsync(string city, CancellationToken ct)
    {
        if (!Cities.TryGetValue(city, out var c)) throw AppException.BadRequest("Desteklenen şehirler: İstanbul, Ankara, İzmir, Adana.");
        try
        {
            return await cache.GetOrSetAsync($"{CacheKeys.Weather}:{city.ToLowerInvariant()}", async () =>
            {
                var url = $"https://api.open-meteo.com/v1/forecast?latitude={c.Lat.ToString(CultureInfo.InvariantCulture)}&longitude={c.Lon.ToString(CultureInfo.InvariantCulture)}" +
                          "&current=temperature_2m,relative_humidity_2m,wind_speed_10m,weather_code&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=Europe%2FIstanbul&forecast_days=4";
                using var doc = JsonDocument.Parse(await http.CreateClient("external").GetStringAsync(url, ct));
                var cur = doc.RootElement.GetProperty("current");
                var daily = doc.RootElement.GetProperty("daily");
                var dates = daily.GetProperty("time").EnumerateArray().Select(x => DateOnly.Parse(x.GetString()!)).ToList();
                var codes = daily.GetProperty("weather_code").EnumerateArray().Select(x => x.GetInt32()).ToList();
                var max = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(x => x.GetDouble()).ToList();
                var min = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(x => x.GetDouble()).ToList();
                var code = cur.GetProperty("weather_code").GetInt32();
                return new WeatherData(c.Name, cur.GetProperty("temperature_2m").GetDouble(), cur.GetProperty("relative_humidity_2m").GetDouble(), cur.GetProperty("wind_speed_10m").GetDouble(),
                    code, Describe(code), dates.Select((d, i) => new WeatherDay(d, min[i], max[i], codes[i], Describe(codes[i]))).ToList(), "Open-Meteo", clock.GetUtcNow().UtcDateTime);
            }, TimeSpan.FromMinutes(20));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            logger.LogWarning("Hava durumu alınamadı: {Error}", ex.Message);
            return null;
        }
    }

    private static string Describe(int code) => code switch
    {
        0 => "Açık", 1 => "Çoğunlukla açık", 2 => "Parçalı bulutlu", 3 => "Kapalı", 45 or 48 => "Sisli",
        51 or 53 or 55 => "Çiseleme", 56 or 57 => "Donan çiseleme", 61 => "Hafif yağmurlu", 63 => "Yağmurlu", 65 => "Kuvvetli yağmur",
        66 or 67 => "Donan yağmur", 71 => "Hafif kar", 73 => "Kar yağışlı", 75 => "Yoğun kar", 77 => "Kar taneleri",
        80 or 81 or 82 => "Sağanak", 85 or 86 => "Kar sağanağı", 95 => "Gök gürültülü", 96 or 99 => "Dolu ile fırtına", _ => "Bilinmiyor",
    };
}
