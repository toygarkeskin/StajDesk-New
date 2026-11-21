namespace StajDesk.Domain;

/// <summary>
/// Şirketin takvim günü Türkiye saatine göredir. Zaman damgaları UTC saklanır; "bugün", gün bazlı raporlar,
/// ciro/vardiya/izin tarih kuralları ve doğum günleri bu sınıf üzerinden Türkiye saatine çevrilerek hesaplanır.
/// Aksi hâlde 00:00–03:00 arasında gün bir önceki güne kayar (ör. günün cirosu "ileri tarihli" sayılır).
/// </summary>
public static class BusinessTime
{
    public static readonly TimeZoneInfo Zone = Resolve();

    private static TimeZoneInfo Resolve()
    {
        // Linux/macOS IANA, Windows kendi adını kullanır; .NET ikisini de çevirir. Bulunamazsa sabit UTC+3 (Türkiye 2016'dan beri yaz saati uygulamıyor).
        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
            if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var tz)) return tz;
        return TimeZoneInfo.CreateCustomTimeZone("TRT", TimeSpan.FromHours(3), "Türkiye", "Türkiye");
    }

    /// <summary>UTC zaman damgasının Türkiye saatindeki karşılığı.</summary>
    public static DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>UTC zaman damgasının düştüğü takvim günü (Türkiye).</summary>
    public static DateOnly DateOf(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));

    /// <summary>Verilen andaki takvim günü (Türkiye).</summary>
    public static DateOnly Today(DateTimeOffset now) => DateOf(now.UtcDateTime);

    /// <summary>Şu anki takvim günü (Türkiye). Saat soyutlaması olmayan yerler (doğrulayıcılar) için.</summary>
    public static DateOnly Today() => Today(DateTimeOffset.UtcNow);

    /// <summary>Türkiye takvim gününün başladığı an, UTC olarak (veritabanı aralık sorguları için).</summary>
    public static DateTime StartOfDayUtc(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), Zone);
}
