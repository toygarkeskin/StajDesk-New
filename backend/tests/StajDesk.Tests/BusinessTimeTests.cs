using System.Text.Json;
using StajDesk.Api.Features;
using StajDesk.Domain;

namespace StajDesk.Tests;

/// <summary>Takvim günü Türkiye saatine göredir; UTC'de bir önceki güne düşen gece saatleri doğru güne sayılmalı.</summary>
public class BusinessTimeTests
{
    [Theory]
    [InlineData("2026-11-16T20:59:00Z", "2026-11-16")] // TR 23:59
    [InlineData("2026-11-16T21:00:00Z", "2026-11-17")] // TR 00:00
    [InlineData("2026-11-16T23:30:00Z", "2026-11-17")] // TR 02:30
    public void DateOf_uses_turkey_calendar_day(string utc, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), BusinessTime.DateOf(DateTime.Parse(utc).ToUniversalTime()));

    [Fact]
    public void StartOfDayUtc_is_turkey_midnight()
    {
        var start = BusinessTime.StartOfDayUtc(new DateOnly(2026, 11, 17));
        Assert.Equal(new DateTime(2026, 11, 16, 21, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(DateTimeKind.Utc, start.Kind);
    }

    [Fact]
    public async Task Revenue_for_today_is_accepted_just_after_midnight_in_turkey()
    {
        // TR 17 Kasım 00:30 = UTC 16 Kasım 21:30. Önceden sunucu günü UTC aldığı için 17 Kasım "ileri tarihli" reddediliyordu.
        var w = new TestWorld { Clock = new FixedClock(new DateTime(2026, 11, 16, 21, 30, 0, DateTimeKind.Utc)) };
        w.Db.Stores.Add(new Store { Code = "M001", Name = "Kadıköy Mağazası", Status = StoreStatus.Open });
        w.Db.FormDefinitions.Add(new FormDefinition
        {
            Key = "store-revenue", Name = "Ciro", Module = "Mağaza Muhasebesi", Kind = FormKinds.Revenue, OwnerDepartment = w.Accounting,
            Fields =
            [
                new FormField { Key = "storeName", Label = "Mağaza", Type = FieldTypes.Text, Required = true },
                new FormField { Key = "date", Label = "Tarih", Type = FieldTypes.Date, Required = true },
                new FormField { Key = "amount", Label = "Toplam", Type = FieldTypes.Currency, Required = true, Min = 0 },
            ],
            Steps = [new WorkflowStep { Name = "Kontrol", ApproverType = ApproverType.ResponsibleDepartment }],
        });
        await w.Db.SaveChangesAsync();

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(new { storeName = "Kadıköy Mağazası", date = "2026-11-17", amount = 1000 }))!;
        var s = await w.Workflow().SubmitAsync("store-revenue", new SubmitRequest(data), w.EmployeeCtx);
        Assert.Contains("Kadıköy Mağazası", s.Summary);
    }
}

/// <summary>Mağaza adları Türkçe harflerle eşleşir; İ ve I ile başlayan mağazalar reddedilmemeli.</summary>
public class TurkishStoreNameTests
{
    [Theory]
    [InlineData("İstanbul Mağaza 01", "İstanbul Mağaza 01")]
    [InlineData("İstanbul Mağaza 01", "istanbul mağaza 01")]
    [InlineData("Isparta Mağaza 01", "Isparta Mağaza 01")]
    [InlineData("Isparta Mağaza 01", "ısparta mağaza 01")]
    [InlineData("Iğdır Mağaza 01", "IĞDIR MAĞAZA 01")]
    [InlineData("Kadıköy Mağazası", "kadıköy mağazası")]
    public void Store_names_match_with_turkish_casing(string stored, string typed) => Assert.True(RevenueService.SameName(stored, typed));

    [Fact]
    public void Different_stores_do_not_match() => Assert.False(RevenueService.SameName("İstanbul Mağaza 01", "İstanbul Mağaza 02"));
}
