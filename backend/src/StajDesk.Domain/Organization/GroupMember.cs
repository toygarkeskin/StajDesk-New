namespace StajDesk.Domain;

public class GroupMember
{
    public int GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public bool IsLead { get; set; }
}
