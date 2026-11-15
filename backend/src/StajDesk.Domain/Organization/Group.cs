namespace StajDesk.Domain;

/// <summary>Destek grupları ve ekipler (ör. IT Destek L1, Ağ ve Altyapı).</summary>
public class Group : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public bool IsSupportGroup { get; set; }
    public ICollection<GroupMember> Members { get; set; } = [];
}
