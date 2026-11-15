namespace StajDesk.Domain;

public static class FieldTypes
{
    public const string Text = "text";
    public const string Textarea = "textarea";
    public const string Number = "number";
    public const string Currency = "currency";
    public const string Date = "date";
    public const string Select = "select";
    public const string MultiSelect = "multiselect";
    public const string Checkbox = "checkbox";
    public const string Email = "email";
    public const string File = "file";
    /// <summary>Aktif bir çalışan seçimi; değer { id, name } olarak saklanır.</summary>
    public const string User = "user";
    /// <summary>Yalnızca bilgilendirme metni; kullanıcıdan değer alınmaz.</summary>
    public const string Description = "description";
    public static readonly string[] All = [Text, Textarea, Number, Currency, Date, Select, MultiSelect, Checkbox, Email, File, User, Description];
}
