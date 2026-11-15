namespace StajDesk.Domain;

public class SurveyResponse
{
    public long Id { get; set; }
    public int SurveyId { get; set; }
    public Survey Survey { get; set; } = null!;
    /// <summary>Anonim anketlerde null; tekrar oylamayı engellemek için RespondentHash tutulur.</summary>
    public Guid? UserId { get; set; }
    public required string RespondentHash { get; set; }
    public string Answers { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
