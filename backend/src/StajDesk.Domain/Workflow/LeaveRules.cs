namespace StajDesk.Domain;

public static class LeaveRules
{
    public static string Label(LeaveType t) => t switch
    {
        LeaveType.Annual => "Yıllık İzin",
        LeaveType.Excuse => "Mazeret İzni",
        LeaveType.Sick => "Hastalık İzni",
        LeaveType.Unpaid => "Ücretsiz İzin",
        LeaveType.Marriage => "Evlilik İzni",
        LeaveType.Bereavement => "Vefat İzni",
        LeaveType.Paternity => "Babalık İzni",
        _ => t.ToString(),
    };

    public static LeaveType? Parse(string? label) =>
        Enum.GetValues<LeaveType>().Cast<LeaveType?>().FirstOrDefault(t => Label(t!.Value) == label || t.ToString() == label);

    // Sabit tarihli resmî tatiller (dini bayramlar yıllara göre değiştiği için ayrıca tanımlanır)
    private static readonly (int Month, int Day)[] FixedHolidays = [(1, 1), (4, 23), (5, 1), (5, 19), (7, 15), (8, 30), (10, 29)];
    private static readonly HashSet<DateOnly> MovableHolidays =
    [
        // 2026 Ramazan Bayramı ve Kurban Bayramı
        new(2026, 3, 20), new(2026, 3, 21), new(2026, 3, 22),
        new(2026, 5, 27), new(2026, 5, 28), new(2026, 5, 29), new(2026, 5, 30),
        // 2027
        new(2027, 3, 9), new(2027, 3, 10), new(2027, 3, 11),
        new(2027, 5, 16), new(2027, 5, 17), new(2027, 5, 18), new(2027, 5, 19),
    ];

    public static bool IsHoliday(DateOnly d) => FixedHolidays.Contains((d.Month, d.Day)) || MovableHolidays.Contains(d);

    /// <summary>Hafta sonu ve resmî tatiller hariç iş günü sayısı.</summary>
    public static int WorkingDays(DateOnly start, DateOnly end)
    {
        if (end < start) return 0;
        var days = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !IsHoliday(d)) days++;
        return days;
    }

    /// <summary>4857 sayılı İş Kanunu md. 53'e göre kıdeme bağlı yıllık izin hakkı.</summary>
    public static int AnnualEntitlement(DateOnly? hireDate, DateOnly asOf)
    {
        if (hireDate is null) return 14;
        var years = asOf.Year - hireDate.Value.Year - (asOf < hireDate.Value.AddYears(asOf.Year - hireDate.Value.Year) ? 1 : 0);
        return years switch { < 1 => 0, < 5 => 14, < 15 => 20, _ => 26 };
    }
}
