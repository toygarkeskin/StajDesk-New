namespace StajDesk.Domain;

/// <summary>Talep yaşam döngüsü ve SLA kuralları: Açık → İşlemde → (Beklemede) → Çözüldü → Kapandı.</summary>
public static class TicketRules
{
    private static readonly Dictionary<TicketStatus, TicketStatus[]> Transitions = new()
    {
        [TicketStatus.Open] = [TicketStatus.InProgress, TicketStatus.Pending, TicketStatus.Resolved, TicketStatus.Closed],
        [TicketStatus.InProgress] = [TicketStatus.Open, TicketStatus.Pending, TicketStatus.Resolved],
        [TicketStatus.Pending] = [TicketStatus.InProgress, TicketStatus.Resolved],
        [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
        [TicketStatus.Closed] = [],
    };

    public static IReadOnlyList<TicketStatus> AllowedNext(TicketStatus from) => Transitions[from];
    public static bool CanTransition(TicketStatus from, TicketStatus to) => Transitions[from].Contains(to);

    /// <summary>Talep sahibi yalnızca çözülmüş talebini onaylayıp kapatabilir veya yeniden açabilir.</summary>
    public static bool RequesterCanTransition(TicketStatus from, TicketStatus to) =>
        from == TicketStatus.Resolved && to is TicketStatus.Closed or TicketStatus.InProgress;

    public static TimeSpan SlaFor(TicketPriority priority) => priority switch
    {
        TicketPriority.Critical => TimeSpan.FromHours(4),
        TicketPriority.High => TimeSpan.FromHours(8),
        TicketPriority.Medium => TimeSpan.FromHours(24),
        _ => TimeSpan.FromHours(72),
    };

    public static string Label(TicketStatus s) => s switch
    {
        TicketStatus.Open => "Açık",
        TicketStatus.InProgress => "İşlemde",
        TicketStatus.Pending => "Beklemede",
        TicketStatus.Resolved => "Çözüldü",
        TicketStatus.Closed => "Kapandı",
        _ => s.ToString(),
    };

    public static string Label(TicketPriority p) => p switch
    {
        TicketPriority.Low => "Düşük",
        TicketPriority.Medium => "Orta",
        TicketPriority.High => "Yüksek",
        TicketPriority.Critical => "Kritik",
        _ => p.ToString(),
    };
}
