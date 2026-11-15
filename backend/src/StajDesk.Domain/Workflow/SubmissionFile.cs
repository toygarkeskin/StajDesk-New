namespace StajDesk.Domain;

public class SubmissionFile
{
    public int SubmissionId { get; set; }
    public FormSubmission Submission { get; set; } = null!;
    public Guid FileId { get; set; }
    public StoredFile File { get; set; } = null!;
    public required string FieldKey { get; set; }
}
