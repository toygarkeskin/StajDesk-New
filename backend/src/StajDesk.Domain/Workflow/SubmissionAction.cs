namespace StajDesk.Domain;

public class SubmissionAction
{
    public long Id { get; set; }
    public int SubmissionId { get; set; }
    public FormSubmission Submission { get; set; } = null!;
    public int? StepIndex { get; set; }
    public string? StepName { get; set; }
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }
    public SubmissionActionType Action { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
