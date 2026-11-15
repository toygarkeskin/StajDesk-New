namespace StajDesk.Domain;

public class Achievement : Entity
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Title { get; set; }
    public string? Description { get; set; }
    public AchievementKind Kind { get; set; }
    public Guid GivenById { get; set; }
    public User GivenBy { get; set; } = null!;
    public int Likes { get; set; }
}
