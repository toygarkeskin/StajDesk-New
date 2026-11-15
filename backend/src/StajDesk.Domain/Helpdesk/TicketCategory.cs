namespace StajDesk.Domain;

public class TicketCategory : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string Color { get; set; } = "#e11d48";
    public bool IsActive { get; set; } = true;
    public int? DefaultGroupId { get; set; }
    public Group? DefaultGroup { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = [];
}
