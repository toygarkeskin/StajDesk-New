namespace StajDesk.Domain;

public enum TicketStatus { Open = 0, InProgress = 1, Pending = 2, Resolved = 3, Closed = 4 }
public enum TicketPriority { Low = 0, Medium = 1, High = 2, Critical = 3 }
public enum TicketType { Incident = 0, Request = 1 }

public class TicketCategory : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string Color { get; set; } = "#e11d48";
    public bool IsActive { get; set; } = true;
    public int? DefaultGroupId { get; set; }
    public Group? DefaultGroup { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = [];
}

public class Ticket : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketType Type { get; set; } = TicketType.Incident;

    public int CategoryId { get; set; }
    public TicketCategory Category { get; set; } = null!;
    public Guid RequesterId { get; set; }
    public User Requester { get; set; } = null!;
    /// <summary>Talep sahibinin talep anındaki departmanı (raporlama ve yönetici görünürlüğü için).</summary>
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int? GroupId { get; set; }
    public Group? Group { get; set; }
    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }
    public int? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public int? AppliedRuleId { get; set; }

    public DateTime DueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public ICollection<TicketComment> Comments { get; set; } = [];
    public ICollection<TicketHistory> History { get; set; } = [];
    public ICollection<TicketAttachment> Attachments { get; set; } = [];

    public bool IsOverdue(DateTime now) => Status is not (TicketStatus.Resolved or TicketStatus.Closed) && DueAt < now;
}

public class TicketComment : Entity
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public required string Body { get; set; }
    public bool IsInternal { get; set; }
}

public class TicketHistory
{
    public long Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }
    public required string Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TicketAttachment
{
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public Guid FileId { get; set; }
    public StoredFile File { get; set; } = null!;
}

/// <summary>GLPI'deki "iş kuralları" benzeri otomatik yönlendirme. Sıra numarasına göre ilk eşleşen kural uygulanır.</summary>
public class RoutingRule : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    // Koşullar (boş olanlar her şeyle eşleşir)
    public int? CategoryId { get; set; }
    public TicketCategory? Category { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string? Keyword { get; set; }
    public TicketPriority? MinPriority { get; set; }
    // Eylemler
    public int? AssignGroupId { get; set; }
    public Group? AssignGroup { get; set; }
    public Guid? AssignUserId { get; set; }
    public User? AssignUser { get; set; }
    public TicketPriority? SetPriority { get; set; }
    public int MatchCount { get; set; }

    public bool Matches(int categoryId, int? departmentId, string title, string description, TicketPriority priority) =>
        IsActive
        && (CategoryId is null || CategoryId == categoryId)
        && (DepartmentId is null || DepartmentId == departmentId)
        && (MinPriority is null || priority >= MinPriority)
        && (string.IsNullOrWhiteSpace(Keyword)
            || title.Contains(Keyword, StringComparison.OrdinalIgnoreCase)
            || description.Contains(Keyword, StringComparison.OrdinalIgnoreCase));
}

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

public class KnowledgeArticle : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Summary { get; set; }
    public required string Content { get; set; }
    public int? CategoryId { get; set; }
    public TicketCategory? Category { get; set; }
    public bool IsFaq { get; set; }
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public bool IsPublished { get; set; } = true;
    public int Views { get; set; }
    public int HelpfulCount { get; set; }
}

public class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long Size { get; set; }
    public required string StoragePath { get; set; }
    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
