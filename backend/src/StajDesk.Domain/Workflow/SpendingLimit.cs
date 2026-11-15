namespace StajDesk.Domain;

/// <summary>Avans ve masraf üst limitleri (Muhasebe → Limitler uygulaması).</summary>
public class SpendingLimit : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Kind { get; set; }
    public required string Label { get; set; }
    public decimal MaxAmount { get; set; }
    public string Currency { get; set; } = "TRY";
}
