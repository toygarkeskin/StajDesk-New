namespace StajDesk.Domain;

public class SurveyQuestion
{
    public required string Id { get; set; }
    public required string Text { get; set; }
    /// <summary>single | multi | text | rating</summary>
    public string Type { get; set; } = "single";
    public List<string> Options { get; set; } = [];
    public bool Required { get; set; } = true;
}
