namespace StajDesk.Domain;

public class Survey : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? EndsAt { get; set; }
    public List<SurveyQuestion> Questions { get; set; } = [];
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<SurveyResponse> Responses { get; set; } = [];
}
