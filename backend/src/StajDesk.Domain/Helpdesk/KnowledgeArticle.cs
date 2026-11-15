namespace StajDesk.Domain;

public class KnowledgeArticle : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Summary { get; set; }
    public required string Content { get; set; }
    public int? CategoryId { get; set; }
    public TicketCategory? Category { get; set; }
    public bool IsFaq { get; set; }
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public bool IsPublished { get; set; } = true;
    public int Views { get; set; }
    public int HelpfulCount { get; set; }
}
