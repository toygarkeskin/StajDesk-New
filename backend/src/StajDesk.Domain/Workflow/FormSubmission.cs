namespace StajDesk.Domain;

public class FormSubmission : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Number { get; set; }
    public int FormDefinitionId { get; set; }
    public FormDefinition FormDefinition { get; set; } = null!;
    public Guid SubmitterId { get; set; }
    public User Submitter { get; set; } = null!;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    /// <summary>Alan anahtarı → değer (JSON).</summary>
    public string Data { get; set; } = "{}";
    public string? Summary { get; set; }
    public decimal? Amount { get; set; }
    public SubmissionStatus Status { get; set; }
    public int CurrentStep { get; set; }
    /// <summary>Mevcut adımda onay verebilecek kullanıcılar.</summary>
    public List<Guid> PendingApproverIds { get; set; } = [];
    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<SubmissionAction> Actions { get; set; } = [];
    public ICollection<SubmissionFile> Files { get; set; } = [];
}
