namespace StajDesk.Domain;

/// <summary>Form oluşturucudaki bir alan. Desteklenen tipler <see cref="FieldTypes"/> içindedir.</summary>
public class FormField
{
    public required string Key { get; set; }
    public required string Label { get; set; }
    public string Type { get; set; } = FieldTypes.Text;
    public bool Required { get; set; }
    public List<string>? Options { get; set; }
    public string? Placeholder { get; set; }
    public string? HelpText { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public int? MaxLength { get; set; }
}
