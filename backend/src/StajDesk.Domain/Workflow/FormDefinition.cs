namespace StajDesk.Domain;

/// <summary>Departman uygulaması. Standart uygulamalar tamamen bu tanımla çalışır; özel hesaplama gerekenler Kind ile ayrı iş mantığına bağlanır.</summary>
public class FormDefinition : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Module { get; set; }
    public string Icon { get; set; } = "FileText";
    public string Color { get; set; } = "#e11d48";
    /// <summary>standard | leave | expense | advance | revenue | shift</summary>
    public string Kind { get; set; } = FormKinds.Standard;
    public int? OwnerDepartmentId { get; set; }
    public Department? OwnerDepartment { get; set; }
    /// <summary>Başvuru yapabilecek roller (boşsa tüm çalışanlar).</summary>
    public List<string> AllowedRoles { get; set; } = [];
    public List<FormField> Fields { get; set; } = [];
    public List<WorkflowStep> Steps { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public ICollection<FormSubmission> Submissions { get; set; } = [];
}
