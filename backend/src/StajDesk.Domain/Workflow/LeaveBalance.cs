namespace StajDesk.Domain;

public class LeaveBalance : Entity
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int Year { get; set; }
    public decimal Entitled { get; set; }
    public decimal CarriedOver { get; set; }
    public decimal Used { get; set; }
    public decimal Remaining => Entitled + CarriedOver - Used;
}
