namespace StajDesk.Domain;

public class WorkflowStep
{
    public required string Name { get; set; }
    public ApproverType ApproverType { get; set; }
    public string? RoleCode { get; set; }
    public Guid? UserId { get; set; }
    /// <summary>ApproverType.FieldUser için: onaylayıcının seçildiği "user" tipli alanın anahtarı.</summary>
    public string? FieldKey { get; set; }
}
