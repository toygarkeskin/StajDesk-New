namespace StajDesk.Domain;

public class TicketComment : Entity
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public required string Body { get; set; }
    public bool IsInternal { get; set; }
}
