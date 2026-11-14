namespace StajDesk.Domain;

public enum AnnouncementKind { Announcement = 0, News = 1 }
public enum ProjectStatus { Planned = 0, Active = 1, OnHold = 2, Completed = 3, Cancelled = 4 }
public enum WorkTaskStatus { Todo = 0, InProgress = 1, Review = 2, Done = 3 }
public enum AchievementKind { Award = 0, Kudos = 1, Milestone = 2 }
public enum StoreStatus { Open = 0, Renovation = 1, Closed = 2 }

public class Announcement : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Summary { get; set; }
    public required string Body { get; set; }
    public AnnouncementKind Kind { get; set; }
    /// <summary>Genel tanıtım sitesinde gösterilir mi?</summary>
    public bool IsPublic { get; set; }
    public bool IsPinned { get; set; }
    public DateTime PublishAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
}

public class Banner : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Subtitle { get; set; }
    public string? ImageUrl { get; set; }
    /// <summary>Görsel yoksa kullanılan marka gradyanı: crimson | midnight | sunset | ocean</summary>
    public string Theme { get; set; } = "crimson";
    public string? LinkUrl { get; set; }
    public string? LinkText { get; set; }
    public int SortOrder { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsLive(DateTime now) => IsActive && (StartsAt is null || StartsAt <= now) && (EndsAt is null || EndsAt >= now);
}

public class CompanyEvent : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string Category { get; set; } = "Kurumsal";
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsPublic { get; set; }
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<EventAttendee> Attendees { get; set; } = [];
}

public class EventAttendee
{
    public int EventId { get; set; }
    public CompanyEvent Event { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class JobPosting : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string? Location { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public required string Description { get; set; }
    public string? Requirements { get; set; }
    public bool IsPublic { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateOnly? ClosesAt { get; set; }
}

public class Achievement : Entity
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Title { get; set; }
    public string? Description { get; set; }
    public AchievementKind Kind { get; set; }
    public Guid GivenById { get; set; }
    public User GivenBy { get; set; } = null!;
    public int Likes { get; set; }
}

public class Survey : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? EndsAt { get; set; }
    public List<SurveyQuestion> Questions { get; set; } = [];
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public ICollection<SurveyResponse> Responses { get; set; } = [];
}

public class SurveyQuestion
{
    public required string Id { get; set; }
    public required string Text { get; set; }
    /// <summary>single | multi | text | rating</summary>
    public string Type { get; set; } = "single";
    public List<string> Options { get; set; } = [];
    public bool Required { get; set; } = true;
}

public class SurveyResponse
{
    public long Id { get; set; }
    public int SurveyId { get; set; }
    public Survey Survey { get; set; } = null!;
    /// <summary>Anonim anketlerde null; tekrar oylamayı engellemek için RespondentHash tutulur.</summary>
    public Guid? UserId { get; set; }
    public required string RespondentHash { get; set; }
    public string Answers { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DirectMessage
{
    public long Id { get; set; }
    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;
    public Guid RecipientId { get; set; }
    public User Recipient { get; set; } = null!;
    public required string Body { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

public class Document : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string Category { get; set; } = "Genel";
    public Guid FileId { get; set; }
    public StoredFile File { get; set; } = null!;
    /// <summary>Boşsa tüm çalışanlar görebilir; doluysa yalnızca bu departman.</summary>
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
    public int Downloads { get; set; }
}

public class Project : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public ProjectStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public ICollection<WorkTask> Tasks { get; set; } = [];
}

public class WorkTask : Entity, IAuditable
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public WorkTaskStatus Status { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public DateOnly? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class Store : Entity, IAuditable
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? City { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public Guid? ManagerId { get; set; }
    public User? Manager { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public StoreStatus Status { get; set; }
    public decimal? AreaSqm { get; set; }
}

public class OnCallShift : Entity, IAuditable
{
    public int Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Team { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? Note { get; set; }
}

public class Notification
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Title { get; set; }
    public required string Message { get; set; }
    /// <summary>ticket | approval | success | warning | info | message | account</summary>
    public string Kind { get; set; } = "info";
    /// <summary>Frontend'de tıklanınca gidilecek yol (ör. /tickets/12).</summary>
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public Guid? SourceEventId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

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
