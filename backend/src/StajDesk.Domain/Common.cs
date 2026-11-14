namespace StajDesk.Domain;

/// <summary>Tüm varlıkların ortak zaman damgaları.</summary>
public abstract class Entity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Bu arayüzü taşıyan varlıklardaki ekleme/güncelleme/silme işlemleri denetim kaydına yazılır.</summary>
public interface IAuditable;

/// <summary>Hata durumunda HTTP katmanının uygun koda çevirdiği domain istisnası.</summary>
public class DomainException(string message) : Exception(message);

public static class Permissions
{
    // Talepler
    public const string TicketsViewAll = "tickets.view_all";
    public const string TicketsViewDepartment = "tickets.view_department";
    public const string TicketsManage = "tickets.manage";
    public const string TicketsDelete = "tickets.delete";
    public const string TicketsConfigure = "tickets.configure"; // kategori, yönlendirme kuralları

    // Envanter
    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";

    // Kullanıcı ve organizasyon
    public const string UsersManage = "users.manage";
    public const string AccountsRequest = "accounts.request";
    public const string AccountsApprove = "accounts.approve";
    public const string RolesManage = "roles.manage";
    public const string OrgManage = "org.manage";

    // İçerik ve iletişim
    public const string ContentManage = "content.manage";
    public const string KnowledgeManage = "kb.manage";
    public const string SurveysManage = "surveys.manage";
    public const string DocumentsManage = "documents.manage";

    // Form ve iş akışları
    public const string FormsManage = "forms.manage";
    public const string SubmissionsViewAll = "submissions.view_all";
    public const string LeaveManage = "leave.manage";
    public const string LimitsManage = "limits.manage";

    // Operasyon
    public const string ProjectsManage = "projects.manage";
    public const string StoresManage = "stores.manage";
    public const string OnCallManage = "oncall.manage";

    // Raporlama ve sistem
    public const string ReportsView = "reports.view";
    public const string ReportsViewDepartment = "reports.view_department";
    public const string AuditView = "audit.view";
    public const string SystemManage = "system.manage"; // sağlık, önbellek temizleme

    public static readonly IReadOnlyList<(string Code, string Group, string Label)> All =
    [
        (TicketsViewAll, "Talepler", "Tüm talepleri görüntüleme"),
        (TicketsViewDepartment, "Talepler", "Departman taleplerini görüntüleme"),
        (TicketsManage, "Talepler", "Talep atama ve durum yönetimi"),
        (TicketsDelete, "Talepler", "Talep silme"),
        (TicketsConfigure, "Talepler", "Kategori ve yönlendirme kuralları"),
        (InventoryView, "Envanter", "Envanteri görüntüleme"),
        (InventoryManage, "Envanter", "Envanter yönetimi, zimmet ve iade"),
        (UsersManage, "Kullanıcılar", "Hesap oluşturma, düzenleme, devre dışı bırakma"),
        (AccountsRequest, "Kullanıcılar", "Hesap açma talebi gönderme (İK)"),
        (AccountsApprove, "Kullanıcılar", "Hesap taleplerini onaylama (IT)"),
        (RolesManage, "Kullanıcılar", "Rol ve yetki yönetimi"),
        (OrgManage, "Organizasyon", "Departman, unvan ve grup yönetimi"),
        (ContentManage, "İçerik", "Duyuru, haber, etkinlik, banner, ilan yönetimi"),
        (KnowledgeManage, "İçerik", "Bilgi bankası ve SSS yönetimi"),
        (SurveysManage, "İçerik", "Anket yönetimi"),
        (DocumentsManage, "İçerik", "Doküman kütüphanesi yönetimi"),
        (FormsManage, "Formlar", "Form ve iş akışı tasarımı"),
        (SubmissionsViewAll, "Formlar", "Tüm form başvurularını görüntüleme"),
        (LeaveManage, "Formlar", "İzin bakiyesi yönetimi"),
        (LimitsManage, "Formlar", "Avans ve masraf limitleri"),
        (ProjectsManage, "Operasyon", "Proje yönetimi"),
        (StoresManage, "Operasyon", "Mağaza yönetimi"),
        (OnCallManage, "Operasyon", "Nöbet çizelgesi yönetimi"),
        (ReportsView, "Raporlar", "Şirket geneli raporlar"),
        (ReportsViewDepartment, "Raporlar", "Departman raporları"),
        (AuditView, "Sistem", "Denetim kayıtları"),
        (SystemManage, "Sistem", "Sistem durumu ve önbellek yönetimi"),
    ];
}

public static class RoleCodes
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string Admin = "ADMIN";
    public const string Ceo = "CEO";
    public const string GeneralManager = "GENERAL_MANAGER";
    public const string Director = "DIRECTOR";
    public const string Manager = "MANAGER";
    public const string DepartmentManager = "DEPARTMENT_MANAGER";
    public const string Hr = "HR";
    public const string It = "IT";
    public const string Employee = "EMPLOYEE";
}
