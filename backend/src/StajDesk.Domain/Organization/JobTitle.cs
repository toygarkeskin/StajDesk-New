namespace StajDesk.Domain;

public class JobTitle : Entity
{
    public int Id { get; set; }
    public required string Name { get; set; }
}
