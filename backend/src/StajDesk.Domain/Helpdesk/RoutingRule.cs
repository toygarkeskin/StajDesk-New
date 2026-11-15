namespace StajDesk.Domain;

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
