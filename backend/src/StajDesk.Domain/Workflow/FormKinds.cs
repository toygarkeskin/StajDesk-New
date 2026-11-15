namespace StajDesk.Domain;

public static class FormKinds
{
    public const string Standard = "standard";
    public const string Leave = "leave";
    public const string Expense = "expense";
    public const string Advance = "advance";
    /// <summary>Mağaza ciro bildirimi: mağaza doğrulama, nakit + kart = toplam, aynı gün tekrarı engeli, ortalama sepet.</summary>
    public const string Revenue = "revenue";
    /// <summary>Vardiya değişikliği: ileri tarih, izin çakışması, takas yapılan kişinin onayı.</summary>
    public const string Shift = "shift";
    public static readonly string[] All = [Standard, Leave, Expense, Advance, Revenue, Shift];
}
