namespace StajDesk.Domain;

public class Banner : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Subtitle { get; set; }
    public string? ImageUrl { get; set; }
    /// <summary>Görsel yoksa kullanılan marka gradyanı: crimson | midnight | sunset | ocean</summary>
    public string Theme { get; set; } = "crimson";
    public string? LinkUrl { get; set; }
    public string? LinkText { get; set; }
    public int SortOrder { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsLive(DateTime now) => IsActive && (StartsAt is null || StartsAt <= now) && (EndsAt is null || EndsAt >= now);
}
