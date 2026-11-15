namespace StajDesk.Domain;

public class TicketAttachment
{
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public Guid FileId { get; set; }
    public StoredFile File { get; set; } = null!;
}
