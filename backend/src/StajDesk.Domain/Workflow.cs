namespace StajDesk.Domain;

public enum SubmissionStatus { Pending = 0, Approved = 1, Rejected = 2, Completed = 3, Cancelled = 4 }
/// <summary>FieldUser: onaylayıcı, formdaki bir "kişi" alanında seçilen çalışandır (ör. vardiya takası yapılacak kişi). Alan boşsa adım atlanır.</summary>
public enum ApproverType { Manager = 0, DepartmentManager = 1, Role = 2, User = 3, ResponsibleDepartment = 4, FieldUser = 5 }
public enum SubmissionActionType { Submitted = 0, Approved = 1, Rejected = 2, Commented = 3, Completed = 4, Cancelled = 5, Skipped = 6 }

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

public class WorkflowStep
{
    public required string Name { get; set; }
    public ApproverType ApproverType { get; set; }
    public string? RoleCode { get; set; }
    public Guid? UserId { get; set; }
    /// <summary>ApproverType.FieldUser için: onaylayıcının seçildiği "user" tipli alanın anahtarı.</summary>
    public string? FieldKey { get; set; }
}

/// <summary>Departman uygulaması. Standart uygulamalar tamamen bu tanımla çalışır; özel hesaplama gerekenler Kind ile ayrı iş mantığına bağlanır.</summary>
public class FormDefinition : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Module { get; set; }
    public string Icon { get; set; } = "FileText";
    public string Color { get; set; } = "#e11d48";
    /// <summary>standard | leave | expense | advance | revenue | shift</summary>
    public string Kind { get; set; } = FormKinds.Standard;
    public int? OwnerDepartmentId { get; set; }
    public Department? OwnerDepartment { get; set; }
    /// <summary>Başvuru yapabilecek roller (boşsa tüm çalışanlar).</summary>
    public List<string> AllowedRoles { get; set; } = [];
    public List<FormField> Fields { get; set; } = [];
    public List<WorkflowStep> Steps { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public ICollection<FormSubmission> Submissions { get; set; } = [];
}

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

public class FormSubmission : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Number { get; set; }
    public int FormDefinitionId { get; set; }
    public FormDefinition FormDefinition { get; set; } = null!;
    public Guid SubmitterId { get; set; }
    public User Submitter { get; set; } = null!;
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    /// <summary>Alan anahtarı → değer (JSON).</summary>
    public string Data { get; set; } = "{}";
    public string? Summary { get; set; }
    public decimal? Amount { get; set; }
    public SubmissionStatus Status { get; set; }
    public int CurrentStep { get; set; }
    /// <summary>Mevcut adımda onay verebilecek kullanıcılar.</summary>
    public List<Guid> PendingApproverIds { get; set; } = [];
    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ICollection<SubmissionAction> Actions { get; set; } = [];
    public ICollection<SubmissionFile> Files { get; set; } = [];
}

public class SubmissionAction
{
    public long Id { get; set; }
    public int SubmissionId { get; set; }
    public FormSubmission Submission { get; set; } = null!;
    public int? StepIndex { get; set; }
    public string? StepName { get; set; }
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }
    public SubmissionActionType Action { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SubmissionFile
{
    public int SubmissionId { get; set; }
    public FormSubmission Submission { get; set; } = null!;
    public Guid FileId { get; set; }
    public StoredFile File { get; set; } = null!;
    public required string FieldKey { get; set; }
}

public enum LeaveType { Annual = 0, Excuse = 1, Sick = 2, Unpaid = 3, Marriage = 4, Bereavement = 5, Paternity = 6 }

public class LeaveBalance : Entity
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int Year { get; set; }
    public decimal Entitled { get; set; }
    public decimal CarriedOver { get; set; }
    public decimal Used { get; set; }
    public decimal Remaining => Entitled + CarriedOver - Used;
}

/// <summary>Avans ve masraf üst limitleri (Muhasebe → Limitler uygulaması).</summary>
public class SpendingLimit : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Kind { get; set; }
    public required string Label { get; set; }
    public decimal MaxAmount { get; set; }
    public string Currency { get; set; } = "TRY";
}

public static class LeaveRules
{
    public static string Label(LeaveType t) => t switch
    {
        LeaveType.Annual => "Yıllık İzin",
        LeaveType.Excuse => "Mazeret İzni",
        LeaveType.Sick => "Hastalık İzni",
        LeaveType.Unpaid => "Ücretsiz İzin",
        LeaveType.Marriage => "Evlilik İzni",
        LeaveType.Bereavement => "Vefat İzni",
        LeaveType.Paternity => "Babalık İzni",
        _ => t.ToString(),
    };

    public static LeaveType? Parse(string? label) =>
        Enum.GetValues<LeaveType>().Cast<LeaveType?>().FirstOrDefault(t => Label(t!.Value) == label || t.ToString() == label);

    // Sabit tarihli resmî tatiller (dini bayramlar yıllara göre değiştiği için ayrıca tanımlanır)
    private static readonly (int Month, int Day)[] FixedHolidays = [(1, 1), (4, 23), (5, 1), (5, 19), (7, 15), (8, 30), (10, 29)];
    private static readonly HashSet<DateOnly> MovableHolidays =
    [
        // 2026 Ramazan Bayramı ve Kurban Bayramı
        new(2026, 3, 20), new(2026, 3, 21), new(2026, 3, 22),
        new(2026, 5, 27), new(2026, 5, 28), new(2026, 5, 29), new(2026, 5, 30),
        // 2027
        new(2027, 3, 9), new(2027, 3, 10), new(2027, 3, 11),
        new(2027, 5, 16), new(2027, 5, 17), new(2027, 5, 18), new(2027, 5, 19),
    ];

    public static bool IsHoliday(DateOnly d) => FixedHolidays.Contains((d.Month, d.Day)) || MovableHolidays.Contains(d);

    /// <summary>Hafta sonu ve resmî tatiller hariç iş günü sayısı.</summary>
    public static int WorkingDays(DateOnly start, DateOnly end)
    {
        if (end < start) return 0;
        var days = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !IsHoliday(d)) days++;
        return days;
    }

    /// <summary>4857 sayılı İş Kanunu md. 53'e göre kıdeme bağlı yıllık izin hakkı.</summary>
    public static int AnnualEntitlement(DateOnly? hireDate, DateOnly asOf)
    {
        if (hireDate is null) return 14;
        var years = asOf.Year - hireDate.Value.Year - (asOf < hireDate.Value.AddYears(asOf.Year - hireDate.Value.Year) ? 1 : 0);
        return years switch { < 1 => 0, < 5 => 14, < 15 => 20, _ => 26 };
    }
}
