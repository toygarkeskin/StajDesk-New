namespace StajDesk.Domain;

/// <summary>Transactional outbox: olay iş verisiyle aynı işlemde kaydedilir, ardından RabbitMQ'ya aktarılır.</summary>
public class OutboxMessage
{
    public long Id { get; set; }
    public Guid EventId { get; set; } = Guid.NewGuid();
    public required string RoutingKey { get; set; }
    public required string Payload { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}
