using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StajDesk.Domain;

namespace StajDesk.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Organizasyon ve kimlik
    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<JobTitle> JobTitles => Set<JobTitle>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LoginChallenge> LoginChallenges => Set<LoginChallenge>();
    public DbSet<AccountRequest> AccountRequests => Set<AccountRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Destek
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<TicketHistory> TicketHistory => Set<TicketHistory>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<RoutingRule> RoutingRules => Set<RoutingRule>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<StoredFile> Files => Set<StoredFile>();

    // Envanter
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetAssignment> AssetAssignments => Set<AssetAssignment>();
    public DbSet<SoftwareLicense> SoftwareLicenses => Set<SoftwareLicense>();
    public DbSet<SoftwareInstallation> SoftwareInstallations => Set<SoftwareInstallation>();

    // Form ve iş akışı
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();
    public DbSet<SubmissionAction> SubmissionActions => Set<SubmissionAction>();
    public DbSet<SubmissionFile> SubmissionFiles => Set<SubmissionFile>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();
    public DbSet<SpendingLimit> SpendingLimits => Set<SpendingLimit>();

    // İletişim ve iş birliği
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<CompanyEvent> Events => Set<CompanyEvent>();
    public DbSet<EventAttendee> EventAttendees => Set<EventAttendee>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<Survey> Surveys => Set<Survey>();
    public DbSet<SurveyResponse> SurveyResponses => Set<SurveyResponse>();
    public DbSet<DirectMessage> Messages => Set<DirectMessage>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<OnCallShift> OnCallShifts => Set<OnCallShift>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Karmaşık listeleri PostgreSQL jsonb sütununda saklar.</summary>
    private static PropertyBuilder<List<T>> JsonColumn<T>(PropertyBuilder<List<T>> p) =>
        p.HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<T>>(v, Json) ?? new List<T>(),
                new ValueComparer<List<T>>(
                    (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                    v => JsonSerializer.Serialize(v, Json).GetHashCode(),
                    v => JsonSerializer.Deserialize<List<T>>(JsonSerializer.Serialize(v, Json), Json)!))
            .HasColumnType("jsonb");

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---------- Organizasyon ----------
        b.Entity<Department>(e =>
        {
            e.ToTable("departments");
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Code).HasMaxLength(30);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<JobTitle>(e =>
        {
            e.ToTable("job_titles");
            e.Property(x => x.Name).HasMaxLength(150);
            e.HasIndex(x => x.Name).IsUnique();
        });
        b.Entity<Role>(e =>
        {
            e.ToTable("roles");
            e.Property(x => x.Code).HasMaxLength(40);
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.Description).HasMaxLength(300);
            e.HasIndex(x => x.Code).IsUnique();
        });
        b.Entity<UserRole>(e =>
        {
            e.ToTable("user_roles");
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.Roles).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId);
        });
        b.Entity<Group>(e =>
        {
            e.ToTable("groups");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(300);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<GroupMember>(e =>
        {
            e.ToTable("group_members");
            e.HasKey(x => new { x.GroupId, x.UserId });
            e.HasOne(x => x.Group).WithMany(g => g.Members).HasForeignKey(x => x.GroupId);
            e.HasOne(x => x.User).WithMany(u => u.Groups).HasForeignKey(x => x.UserId);
        });
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Ignore(x => x.FullName);
            e.Property(x => x.EmployeeNo).HasMaxLength(30);
            e.Property(x => x.FirstName).HasMaxLength(60);
            e.Property(x => x.LastName).HasMaxLength(60);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Location).HasMaxLength(120);
            e.Property(x => x.Bio).HasMaxLength(500);
            e.Property(x => x.DeactivationReason).HasMaxLength(300);
            e.Property(x => x.EmploymentType).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.EmployeeNo).IsUnique();
            e.HasIndex(x => x.DepartmentId);
            e.HasOne(x => x.Department).WithMany(d => d.Users).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.JobTitle).WithMany().HasForeignKey(x => x.JobTitleId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<LoginChallenge>(e =>
        {
            e.ToTable("login_challenges");
            e.Property(x => x.CodeHash).HasMaxLength(100);
            e.Property(x => x.Ip).HasMaxLength(64);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.Property(x => x.TokenHash).HasMaxLength(100);
            e.Property(x => x.ReplacedByHash).HasMaxLength(100);
            e.Property(x => x.CreatedByIp).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<AccountRequest>(e =>
        {
            e.ToTable("account_requests");
            e.Property(x => x.FirstName).HasMaxLength(60);
            e.Property(x => x.LastName).HasMaxLength(60);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.EmployeeNo).HasMaxLength(30);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.Property(x => x.RejectReason).HasMaxLength(500);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.EmploymentType).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ProcessedBy).WithMany().HasForeignKey(x => x.ProcessedById).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.JobTitle).WithMany().HasForeignKey(x => x.JobTitleId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.Property(x => x.UserName).HasMaxLength(150);
            e.Property(x => x.Action).HasMaxLength(40);
            e.Property(x => x.EntityType).HasMaxLength(60);
            e.Property(x => x.EntityId).HasMaxLength(60);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.Changes).HasColumnType("jsonb");
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        // ---------- Destek ----------
        b.Entity<TicketCategory>(e =>
        {
            e.ToTable("ticket_categories");
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.Color).HasMaxLength(9);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasOne(x => x.DefaultGroup).WithMany().HasForeignKey(x => x.DefaultGroupId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Ticket>(e =>
        {
            e.ToTable("tickets");
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(8000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Category).WithMany(c => c.Tickets).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Asset).WithMany(a => a.Tickets).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.RequesterId);
            e.HasIndex(x => x.AssigneeId);
            e.HasIndex(x => x.DepartmentId);
            e.HasIndex(x => x.CreatedAt);
        });
        b.Entity<TicketComment>(e =>
        {
            e.ToTable("ticket_comments");
            e.Property(x => x.Body).HasMaxLength(4000);
            e.HasOne(x => x.Ticket).WithMany(t => t.Comments).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<TicketHistory>(e =>
        {
            e.ToTable("ticket_history");
            e.Property(x => x.Field).HasMaxLength(40);
            e.Property(x => x.OldValue).HasMaxLength(200);
            e.Property(x => x.NewValue).HasMaxLength(200);
            e.HasOne(x => x.Ticket).WithMany(t => t.History).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<TicketAttachment>(e =>
        {
            e.ToTable("ticket_attachments");
            e.HasKey(x => new { x.TicketId, x.FileId });
            e.HasOne(x => x.Ticket).WithMany(t => t.Attachments).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<RoutingRule>(e =>
        {
            e.ToTable("routing_rules");
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Keyword).HasMaxLength(80);
            e.Property(x => x.MinPriority).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.SetPriority).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AssignGroup).WithMany().HasForeignKey(x => x.AssignGroupId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AssignUser).WithMany().HasForeignKey(x => x.AssignUserId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<KnowledgeArticle>(e =>
        {
            e.ToTable("knowledge_articles");
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Summary).HasMaxLength(400);
            e.Property(x => x.Content).HasMaxLength(20000);
            e.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StoredFile>(e =>
        {
            e.ToTable("files");
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(120);
            e.Property(x => x.StoragePath).HasMaxLength(400);
            e.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Envanter ----------
        b.Entity<Asset>(e =>
        {
            e.ToTable("assets");
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.AssetTag).HasMaxLength(40);
            e.HasIndex(x => x.AssetTag).IsUnique();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Manufacturer).HasMaxLength(80);
            e.Property(x => x.Model).HasMaxLength(120);
            e.Property(x => x.SerialNumber).HasMaxLength(80);
            e.Property(x => x.Location).HasMaxLength(150);
            e.Property(x => x.PurchaseCost).HasPrecision(14, 2);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AssignedTo).WithMany().HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<AssetAssignment>(e =>
        {
            e.ToTable("asset_assignments");
            e.Property(x => x.Note).HasMaxLength(500);
            e.Property(x => x.ReturnCondition).HasMaxLength(200);
            e.HasOne(x => x.Asset).WithMany(a => a.Assignments).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedBy).WithMany().HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReturnedBy).WithMany().HasForeignKey(x => x.ReturnedById).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SoftwareLicense>(e =>
        {
            e.ToTable("software_licenses");
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Vendor).HasMaxLength(100);
            e.Property(x => x.Version).HasMaxLength(40);
            e.Property(x => x.Cost).HasPrecision(14, 2);
            e.Property(x => x.Notes).HasMaxLength(1000);
        });
        b.Entity<SoftwareInstallation>(e =>
        {
            e.ToTable("software_installations");
            e.HasIndex(x => new { x.LicenseId, x.AssetId }).IsUnique();
            e.HasOne(x => x.License).WithMany(l => l.Installations).HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Asset).WithMany(a => a.Software).HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- Form ve iş akışı ----------
        b.Entity<FormDefinition>(e =>
        {
            e.ToTable("form_definitions");
            e.Property(x => x.Key).HasMaxLength(60);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Module).HasMaxLength(80);
            e.Property(x => x.Icon).HasMaxLength(40);
            e.Property(x => x.Color).HasMaxLength(9);
            e.Property(x => x.Kind).HasMaxLength(20);
            JsonColumn(e.Property(x => x.Fields));
            JsonColumn(e.Property(x => x.Steps));
            e.HasOne(x => x.OwnerDepartment).WithMany().HasForeignKey(x => x.OwnerDepartmentId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<FormSubmission>(e =>
        {
            e.ToTable("form_submissions");
            e.Property(x => x.Number).HasMaxLength(30);
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Data).HasColumnType("jsonb");
            e.Property(x => x.Summary).HasMaxLength(300);
            e.Property(x => x.Amount).HasPrecision(14, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.SubmitterId);
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.FormDefinition).WithMany(f => f.Submissions).HasForeignKey(x => x.FormDefinitionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Submitter).WithMany().HasForeignKey(x => x.SubmitterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<SubmissionAction>(e =>
        {
            e.ToTable("submission_actions");
            e.Property(x => x.StepName).HasMaxLength(120);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Submission).WithMany(s => s.Actions).HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<SubmissionFile>(e =>
        {
            e.ToTable("submission_files");
            e.HasKey(x => new { x.SubmissionId, x.FileId });
            e.Property(x => x.FieldKey).HasMaxLength(60);
            e.HasOne(x => x.Submission).WithMany(s => s.Files).HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LeaveBalance>(e =>
        {
            e.ToTable("leave_balances");
            e.Ignore(x => x.Remaining);
            e.Property(x => x.Entitled).HasPrecision(6, 1);
            e.Property(x => x.CarriedOver).HasPrecision(6, 1);
            e.Property(x => x.Used).HasPrecision(6, 1);
            e.HasIndex(x => new { x.UserId, x.Year }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<SpendingLimit>(e =>
        {
            e.ToTable("spending_limits");
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.Label).HasMaxLength(100);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.MaxAmount).HasPrecision(14, 2);
            e.HasIndex(x => x.Kind).IsUnique();
        });

        // ---------- İletişim ----------
        b.Entity<Announcement>(e =>
        {
            e.ToTable("announcements");
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Summary).HasMaxLength(400);
            e.Property(x => x.Body).HasMaxLength(10000);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.PublishAt);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Banner>(e =>
        {
            e.ToTable("banners");
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Subtitle).HasMaxLength(300);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            e.Property(x => x.Theme).HasMaxLength(20);
            e.Property(x => x.LinkUrl).HasMaxLength(500);
            e.Property(x => x.LinkText).HasMaxLength(60);
        });
        b.Entity<CompanyEvent>(e =>
        {
            e.ToTable("events");
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Location).HasMaxLength(150);
            e.Property(x => x.Category).HasMaxLength(40);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<EventAttendee>(e =>
        {
            e.ToTable("event_attendees");
            e.HasKey(x => new { x.EventId, x.UserId });
            e.HasOne(x => x.Event).WithMany(ev => ev.Attendees).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<JobPosting>(e =>
        {
            e.ToTable("job_postings");
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Location).HasMaxLength(120);
            e.Property(x => x.Description).HasMaxLength(5000);
            e.Property(x => x.Requirements).HasMaxLength(5000);
            e.Property(x => x.EmploymentType).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Achievement>(e =>
        {
            e.ToTable("achievements");
            e.Property(x => x.Title).HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.GivenBy).WithMany().HasForeignKey(x => x.GivenById).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Survey>(e =>
        {
            e.ToTable("surveys");
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            JsonColumn(e.Property(x => x.Questions));
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<SurveyResponse>(e =>
        {
            e.ToTable("survey_responses");
            e.Property(x => x.RespondentHash).HasMaxLength(100);
            e.Property(x => x.Answers).HasColumnType("jsonb");
            e.HasIndex(x => new { x.SurveyId, x.RespondentHash }).IsUnique();
            e.HasOne(x => x.Survey).WithMany(s => s.Responses).HasForeignKey(x => x.SurveyId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<DirectMessage>(e =>
        {
            e.ToTable("messages");
            e.Property(x => x.Body).HasMaxLength(4000);
            e.HasIndex(x => new { x.RecipientId, x.ReadAt });
            e.HasIndex(x => new { x.SenderId, x.RecipientId, x.CreatedAt });
            e.HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Recipient).WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Document>(e =>
        {
            e.ToTable("documents");
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Category).HasMaxLength(60);
            e.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Project>(e =>
        {
            e.ToTable("projects");
            e.Property(x => x.Code).HasMaxLength(20);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<WorkTask>(e =>
        {
            e.ToTable("tasks");
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.AssigneeId);
            e.HasOne(x => x.Project).WithMany(p => p.Tasks).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Store>(e =>
        {
            e.ToTable("stores");
            e.Property(x => x.Code).HasMaxLength(20);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.City).HasMaxLength(60);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.AreaSqm).HasPrecision(8, 1);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Manager).WithMany().HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<OnCallShift>(e =>
        {
            e.ToTable("on_call_shifts");
            e.Property(x => x.Team).HasMaxLength(60);
            e.Property(x => x.Note).HasMaxLength(300);
            e.HasIndex(x => new { x.StartsAt, x.EndsAt });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Notification>(e =>
        {
            e.ToTable("notifications");
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.Property(x => x.Kind).HasMaxLength(30);
            e.Property(x => x.Link).HasMaxLength(300);
            e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
            // Aynı olay iki kez işlenirse aynı kullanıcıya ikinci bildirim oluşmasın (idempotent tüketici)
            e.HasIndex(x => new { x.SourceEventId, x.UserId }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.Property(x => x.RoutingKey).HasMaxLength(80);
            e.Property(x => x.Payload).HasColumnType("jsonb");
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.HasIndex(x => x.PublishedAt);
        });
    }
}
