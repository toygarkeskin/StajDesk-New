namespace StajDesk.Domain;

/// <summary>Sistem rolü. Organizasyonel unvandan bağımsızdır; yetkiler izin kodlarıyla tanımlanır.</summary>
public class Role : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public List<string> Permissions { get; set; } = [];
    public ICollection<UserRole> Users { get; set; } = [];
}
