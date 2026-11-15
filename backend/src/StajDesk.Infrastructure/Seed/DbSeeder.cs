using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Infrastructure.Seed;

/// <param name="OrganizationFile">İsteğe bağlı gerçek organizasyon dosyası (depoya girmez). Yoksa gömülü anonim örnek kullanılır.</param>
public record SeedOptions(string DefaultPassword, string UploadRoot, string? OrganizationFile = null);

/// <summary>Boş veritabanını tüm modülleri gösteren tutarlı demo verisiyle doldurur.</summary>
public class DbSeeder(AppDbContext db, IPasswordHasher<User> hasher, ILogger<DbSeeder> logger)
{
    private readonly Random _rnd = new(2026);
    private readonly DateTime _now = DateTime.UtcNow;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private record OrgFile(List<OrgDept> Departments, List<string> Titles);
    private record OrgDept(string Name, string Type);

    public async Task SeedAsync(SeedOptions options, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            await UpgradeFormKindsAsync(ct);
            return;
        }
        logger.LogInformation("Boş veritabanı: başlangıç verisi oluşturuluyor");

        var org = LoadOrganization(options.OrganizationFile);
        var depts = await SeedOrganization(org, ct);
        var titles = await db.JobTitles.ToDictionaryAsync(t => t.Name, ct);
        var roles = await SeedRoles(ct);
        var people = await SeedUsers(depts, titles, roles, options.DefaultPassword, ct);
        var groups = await SeedGroups(depts, people, ct);
        var categories = await SeedHelpdeskConfig(depts, groups, people, ct);
        var assets = await SeedInventory(depts, people, ct);
        await SeedTickets(categories, groups, people, assets, ct);
        await SeedKnowledge(categories, people, ct);
        var forms = await SeedForms(depts, ct);
        await SeedWorkflowData(forms, people, ct);
        await SeedContent(depts, people, ct);
        await SeedOperations(depts, people, options.UploadRoot, ct);
        logger.LogInformation("Başlangıç verisi hazır: {Users} kullanıcı, {Depts} departman, {Forms} form", people.All.Count, depts.Count, forms.Count);
    }

    /// <summary>
    /// Mevcut veritabanında sonradan özel iş mantığı kazanan formları (ör. Ciro, Vardiya) günceller.
    /// Yalnızca hâlâ "standard" türündeki sistem formlarına dokunur; yöneticinin özelleştirdiği türler korunur.
    /// </summary>
    private async Task UpgradeFormKindsAsync(CancellationToken ct)
    {
        var special = FormCatalog.All.Where(d => d.Kind != FormKinds.Standard).ToDictionary(d => d.Key);
        var keys = special.Keys.ToList();
        var forms = await db.FormDefinitions.Where(f => keys.Contains(f.Key) && f.Kind == FormKinds.Standard).ToListAsync(ct);
        foreach (var f in forms)
        {
            var d = special[f.Key];
            f.Kind = d.Kind;
            f.Description = d.Description;
            f.Fields = d.Fields;
            f.Steps = d.Steps;
            logger.LogInformation("Form türü güncellendi {FormKey} → {Kind}", f.Key, d.Kind);
        }
        if (forms.Count > 0) await db.SaveChangesAsync(ct);
    }

    private OrgFile LoadOrganization(string? file)
    {
        if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
        {
            logger.LogInformation("Organizasyon verisi dış dosyadan yükleniyor");
            return JsonSerializer.Deserialize<OrgFile>(File.ReadAllText(file), Json)!;
        }
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("StajDesk.Infrastructure.Seed.organization.json")
                      ?? throw new InvalidOperationException("organization.json bulunamadı");
        return JsonSerializer.Deserialize<OrgFile>(s, Json)!;
    }

    // =============================================================== ORGANİZASYON
    private async Task<Dictionary<string, Department>> SeedOrganization(OrgFile org, CancellationToken ct)
    {
        var list = org.Departments.Select(d => (d.Name, Type: Enum.Parse<DepartmentType>(d.Type))).ToList();
        // CSV'de bulunmayan ancak departman uygulamaları için gereken merkez birimleri
        list.Add((FormCatalog.Depts.Legal, DepartmentType.HeadOffice));
        list.Add((FormCatalog.Depts.Quality, DepartmentType.HeadOffice));

        int hq = 0, store = 0, region = 0;
        var result = new Dictionary<string, Department>();
        foreach (var (name, type) in list.DistinctBy(x => x.Name))
        {
            var code = type switch
            {
                DepartmentType.Store => $"MGZ-{++store:000}",
                DepartmentType.Region => $"BLG-{++region:00}",
                _ => $"GM-{++hq:00}",
            };
            result[name] = new Department { Name = name, Type = type, Code = code };
        }
        db.Departments.AddRange(result.Values);

        var titles = org.Titles.Concat(["Hukuk Müşaviri", "Kalite Güvence Uzmanı"]).Distinct();
        db.JobTitles.AddRange(titles.Select(t => new JobTitle { Name = t }));
        await db.SaveChangesAsync(ct);
        return result;
    }

    private async Task<Dictionary<string, Role>> SeedRoles(CancellationToken ct)
    {
        var all = Permissions.All.Select(p => p.Code).ToList();
        var defs = new (string Code, string Name, string Desc, List<string> Perms)[]
        {
            (RoleCodes.SuperAdmin, "Super Admin", "Sistemdeki tüm yetkiler; rol yönetimi dahil.", all),
            (RoleCodes.Admin, "Admin", "Sistem yöneticisi.", all.Where(p => p != Permissions.RolesManage).ToList()),
            (RoleCodes.Ceo, "CEO", "Şirket geneli raporlar ve görünürlük.",
                [Permissions.ReportsView, Permissions.TicketsViewAll, Permissions.SubmissionsViewAll, Permissions.InventoryView, Permissions.AuditView]),
            (RoleCodes.GeneralManager, "Genel Müdür", "Şirket geneli raporlar ve onaylar.",
                [Permissions.ReportsView, Permissions.TicketsViewAll, Permissions.SubmissionsViewAll, Permissions.InventoryView, Permissions.ProjectsManage]),
            (RoleCodes.Director, "Direktör", "Direktörlüğe bağlı birimlerin raporları ve formları.",
                [Permissions.ReportsViewDepartment, Permissions.TicketsViewDepartment, Permissions.ProjectsManage, Permissions.FormsManage]),
            (RoleCodes.Manager, "Müdür", "Bağlı ekip ve mağaza yönetimi.",
                [Permissions.ReportsViewDepartment, Permissions.TicketsViewDepartment]),
            (RoleCodes.DepartmentManager, "Departman Yöneticisi", "Departman raporları, projeler ve departman formları.",
                [Permissions.ReportsViewDepartment, Permissions.TicketsViewDepartment, Permissions.ProjectsManage, Permissions.FormsManage]),
            (RoleCodes.Hr, "İK", "Hesap talebi, izin bakiyeleri, iç iletişim ve anketler.",
                [Permissions.AccountsRequest, Permissions.LeaveManage, Permissions.ContentManage, Permissions.SurveysManage, Permissions.DocumentsManage, Permissions.ReportsViewDepartment]),
            (RoleCodes.It, "IT", "Destek talepleri, envanter, hesap açma ve sistem yönetimi.",
                [Permissions.TicketsViewAll, Permissions.TicketsManage, Permissions.TicketsConfigure, Permissions.InventoryView, Permissions.InventoryManage,
                 Permissions.UsersManage, Permissions.AccountsApprove, Permissions.KnowledgeManage, Permissions.SystemManage, Permissions.OnCallManage, Permissions.ReportsViewDepartment]),
            (RoleCodes.Employee, "Çalışan", "Temel portal erişimi: talep, form, iletişim modülleri.", []),
        };
        var roles = defs.ToDictionary(d => d.Code, d => new Role { Code = d.Code, Name = d.Name, Description = d.Desc, Permissions = d.Perms, IsSystem = true });
        db.Roles.AddRange(roles.Values);
        await db.SaveChangesAsync(ct);
        return roles;
    }

    // =============================================================== KULLANICILAR
    public sealed class People
    {
        public required User SuperAdmin, Admin, Ceo, Gm, ItDirector, HrDirector, HrSpecialist, Manager, Employee, StoreManager, NewHire;
        public List<User> All { get; } = [];
        public List<User> ItAgents { get; } = [];
        public List<User> Staff => All.Where(u => u.Email != NewHire.Email).ToList();
    }

    private static readonly string[] FirstNames = ["Ahmet", "Ayşe", "Mehmet", "Fatma", "Can", "Elif", "Mert", "Zeynep", "Burak", "Selin", "Emre", "Deniz", "Gizem", "Oğuz", "Ece",
        "Kerem", "Buse", "Tolga", "Derya", "Serkan", "Melis", "Onur", "İrem", "Barış", "Pınar", "Cem", "Ebru", "Hakan", "Sinem", "Umut", "Nazlı", "Kaan", "Tuğba", "Arda", "Merve", "Volkan", "Esra", "Murat", "Aslı", "Gökhan"];
    private static readonly string[] LastNames = ["Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Yıldız", "Aydın", "Öztürk", "Arslan", "Doğan", "Kılıç", "Aslan", "Çetin", "Koç", "Kurt",
        "Özdemir", "Polat", "Erdoğan", "Aksoy", "Tekin", "Güneş", "Bulut", "Korkmaz", "Özkan", "Uçar", "Karaca", "Ateş", "Bozkurt", "Turan", "Eren"];

    private async Task<People> SeedUsers(Dictionary<string, Department> depts, Dictionary<string, JobTitle> titles, Dictionary<string, Role> roles, string password, CancellationToken ct)
    {
        var used = new HashSet<string>();
        var empNo = 1000;
        // Demo mağazaları şehre göre seçilir (gerçek veya anonim organizasyon dosyasıyla aynı şekilde çalışır)
        string? StoreIn(string city) => depts.Values.Where(d => d.Type == DepartmentType.Store && d.Name.StartsWith(city, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(d => d.Name).Select(d => d.Name).FirstOrDefault();
        var mainStore = StoreIn("Ankara") ?? depts.Values.Where(d => d.Type == DepartmentType.Store).OrderBy(d => d.Name).Select(d => d.Name).First();

        User Make(string first, string last, string email, string dept, string title, params string[] roleCodes)
        {
            used.Add(first + last);
            var u = new User
            {
                FirstName = first, LastName = last, Email = email, EmployeeNo = $"DP{++empNo}",
                Department = depts[dept], JobTitle = titles.GetValueOrDefault(title), MustChangePassword = false,
                Phone = $"0 5{_rnd.Next(30, 56)} {_rnd.Next(100, 999)} {_rnd.Next(10, 99)} {_rnd.Next(10, 99)}",
                BirthDate = BirthDate(), HireDate = DateOnly.FromDateTime(_now.AddDays(-_rnd.Next(400, 4500))),
                EmploymentType = title.Contains("Part-time") ? EmploymentType.PartTime : EmploymentType.FullTime,
                Location = depts[dept].Type == DepartmentType.Store ? depts[dept].Name : "Genel Müdürlük - İstanbul",
                CreatedAt = _now.AddDays(-_rnd.Next(30, 400)),
            };
            u.PasswordHash = hasher.HashPassword(u, password);
            foreach (var code in roleCodes.Append(RoleCodes.Employee).Distinct()) u.Roles.Add(new UserRole { Role = roles[code] });
            return u;
        }

        (string F, string L) RandomName()
        {
            for (var i = 0; i < 200; i++)
            {
                var f = FirstNames[_rnd.Next(FirstNames.Length)];
                var l = LastNames[_rnd.Next(LastNames.Length)];
                if (used.Add(f + l)) return (f, l);
            }
            return ("Personel", $"No{used.Count}");
        }

        string Mail(string f, string l) => $"{Slug(f)}.{Slug(l)}@devoplus.local";

        const string It = FormCatalog.Depts.It, Hr = FormCatalog.Depts.Hr;
        var p = new People
        {
            SuperAdmin = Make("Sistem", "Yöneticisi", "superadmin@devoplus.local", It, "BT Altyapı ve Yazılım Geliştirme Yöneticisi", RoleCodes.SuperAdmin, RoleCodes.It),
            Admin = Make("Kerem", "Aksoy", "admin@devoplus.local", It, "Kurumsal Uygulamalar Yöneticisi", RoleCodes.Admin, RoleCodes.It),
            Ceo = Make("Levent", "Karaca", "ceo@devoplus.local", "Genel Müdür / CEO", "CEO", RoleCodes.Ceo),
            Gm = Make("Nur", "Erdem", "genelmudur@devoplus.local", FormCatalog.Depts.Management, "Genel Müdür", RoleCodes.GeneralManager),
            ItDirector = Make("Serdar", "Uysal", "it.direktor@devoplus.local", It, "Bilgi Teknolojileri ve Dijital Dönüşüm Direktörü", RoleCodes.Director, RoleCodes.It),
            HrDirector = Make("Gülşen", "Taş", "ik.direktor@devoplus.local", Hr, "İnsan Kaynakları Direktörü", RoleCodes.Director, RoleCodes.Hr),
            HrSpecialist = Make("Ece", "Kurt", "ik.uzman@devoplus.local", Hr, "İnsan Kaynakları Uzmanı", RoleCodes.Hr),
            Manager = Make("Hakan", "Bulut", "mudur@devoplus.local", FormCatalog.Depts.Accounting, "Mali İşler Müdürü", RoleCodes.DepartmentManager),
            Employee = Make("Ahmet", "Yılmaz", "calisan@devoplus.local", FormCatalog.Depts.Accounting, "Muhasebe Uzmanı"),
            StoreManager = Make("Derya", "Koç", "magaza.yonetici@devoplus.local", mainStore, "Mağaza Yöneticisi", RoleCodes.Manager),
            NewHire = Make("Arda", "Turan", "yeni.personel@devoplus.local", FormCatalog.Depts.Marketing, "Pazarlama Uzman Yardımcısı"),
        };
        p.NewHire.MustChangePassword = true;
        p.NewHire.HireDate = DateOnly.FromDateTime(_now.AddDays(-2));
        p.All.AddRange([p.SuperAdmin, p.Admin, p.Ceo, p.Gm, p.ItDirector, p.HrDirector, p.HrSpecialist, p.Manager, p.Employee, p.StoreManager, p.NewHire]);

        // IT destek ekibi
        foreach (var (f, l, mail, title) in new[]
        {
            ("Elif", "Aydın", "it.destek@devoplus.local", "Bilgi Teknolojileri Destek Uzmanı"),
            ("Murat", "Kaya", "murat.kaya@devoplus.local", "Bilgi Teknolojileri Destek Kıdemli Uzmanı"),
            ("Zeynep", "Demir", "zeynep.demir@devoplus.local", "Sistem ve Network Kıdemli Uzmanı"),
            ("Onur", "Çetin", "onur.cetin@devoplus.local", "Yazılım ve Proje Geliştirme Uzmanı"),
        })
        {
            var u = Make(f, l, mail, It, title, RoleCodes.It);
            p.ItAgents.Add(u);
            p.All.Add(u);
        }
        p.ItAgents.Add(p.Admin);

        // Merkez departmanlar: yönetici + ekip
        var teams = new (string Dept, string Lead, string LeadRole, string[] Staff)[]
        {
            (FormCatalog.Depts.Accounting, "", "", ["Genel Muhasebe Uzmanı", "Mağazacılık Muhasebe Uzmanı"]),
            ("Finans Departmanı", "Finans Yöneticisi (V)", RoleCodes.DepartmentManager, ["Finans Uzmanı", "Banka İşlemleri Uzmanı"]),
            (FormCatalog.Depts.Marketing, "Pazarlama ve CRM Yöneticisi", RoleCodes.DepartmentManager, ["CRM Uzman Yardımcısı", "Pazarlama Uzmanı"]),
            (FormCatalog.Depts.Purchasing, "Satın Alma Müdürü", RoleCodes.DepartmentManager, ["Buyer", "Asistan Buyer"]),
            (FormCatalog.Depts.Logistics, "Lojistik Müdürü", RoleCodes.DepartmentManager, ["Lojistik Operasyon Uzmanı", "Depo Görevlisi"]),
            ("Planlama ve Alokasyon Departmanı", "Planlama ve Alokasyon Müdürü", RoleCodes.DepartmentManager, ["Alokasyon Uzmanı", "Planlama Uzmanı"]),
            (FormCatalog.Depts.Strategy, "Yönetişim Ofisi Yöneticisi", RoleCodes.DepartmentManager, ["Yönetim Sistemleri ve Süreç Geliştirme Uzmanı"]),
            ("Tasarım Departmanı", "Tasarım Direktörü", RoleCodes.Director, ["Kıdemli Tasarımcı", "Tasarımcı"]),
            (FormCatalog.Depts.Product, "Ürün Direktörü", RoleCodes.Director, ["Merchandiser", "Garment Technologist"]),
            (FormCatalog.Depts.Export, "Yurtdışı Satış ve Operasyon Müdürü", RoleCodes.DepartmentManager, ["Yurtdışı Satış ve Operasyon Uzmanı"]),
            (FormCatalog.Depts.SalesSupport, "Perakende Satış Müdürü", RoleCodes.DepartmentManager, ["Satış Destek Danışmanı", "Satış Raporlama ve Analiz Uzman Yardımcısı"]),
            ("Kurumsal Satış ve Zincir Mağazalar Departmanı", "Kurumsal Satış ve Zincir Mağazalar Koordinatörü", RoleCodes.DepartmentManager, ["Zincir Mağazalar Operasyon Kıdemli Uzmanı"]),
            (FormCatalog.Depts.Legal, "Hukuk Müşaviri", RoleCodes.DepartmentManager, ["Departman Asistanı"]),
            (FormCatalog.Depts.Quality, "Stok Kontrol ve Ürün İnceleme Yöneticisi", RoleCodes.DepartmentManager, ["Ürün İnceleme Uzmanı", "Kalite Güvence Uzmanı"]),
            (Hr, "", "", ["Bordro ve Özlük Uzmanı", "Eğitim ve Gelişim Uzmanı", "İdari İşler Uzmanı"]),
            (It, "", "", []),
        };

        var leads = new Dictionary<string, User>
        {
            [It] = p.ItDirector, [Hr] = p.HrDirector, [FormCatalog.Depts.Accounting] = p.Manager,
            [FormCatalog.Depts.Management] = p.Gm, ["Genel Müdür / CEO"] = p.Ceo,
        };
        foreach (var t in teams)
        {
            if (!leads.TryGetValue(t.Dept, out var lead))
            {
                var (f, l) = RandomName();
                lead = Make(f, l, Mail(f, l), t.Dept, t.Lead, t.LeadRole);
                leads[t.Dept] = lead;
                p.All.Add(lead);
            }
            foreach (var title in t.Staff)
            {
                var (f, l) = RandomName();
                var hrRole = t.Dept == Hr ? RoleCodes.Hr : RoleCodes.Employee;
                var u = Make(f, l, Mail(f, l), t.Dept, title, hrRole);
                u.Manager = lead;
                p.All.Add(u);
            }
        }

        // Mağazalar ve bölge yönetimi
        var region = depts.Values.First(d => d.Type == DepartmentType.Region);
        var (rf, rl) = RandomName();
        var regionManager = Make(rf, rl, Mail(rf, rl), region.Name, "Bölge Müdürü", RoleCodes.Manager);
        p.All.Add(regionManager);
        leads[region.Name] = regionManager;
        p.StoreManager.Manager = regionManager;
        leads[mainStore] = p.StoreManager;
        var storeNames = new[] { "Ankara", "İstanbul", "İzmir", "Antalya", "Gaziantep", "Kayseri" }.Select(StoreIn).OfType<string>().Prepend(mainStore).Distinct().ToList();
        foreach (var storeName in storeNames)
        {
            if (!leads.TryGetValue(storeName, out var sm))
            {
                var (f, l) = RandomName();
                sm = Make(f, l, Mail(f, l), storeName, "Mağaza Yöneticisi", RoleCodes.Manager);
                sm.Manager = regionManager;
                leads[storeName] = sm;
                p.All.Add(sm);
            }
            foreach (var title in new[] { "Uzman Satış Danışmanı", "Satış Danışmanı", "Kasiyer" })
            {
                var (f, l) = RandomName();
                var u = Make(f, l, Mail(f, l), storeName, title);
                u.Manager = sm;
                p.All.Add(u);
            }
        }

        // Hiyerarşi: çalışan → departman yöneticisi → Genel Müdür → CEO
        p.Gm.Manager = p.Ceo;
        foreach (var (deptName, lead) in leads)
        {
            depts[deptName].Manager = lead;
            if (lead != p.Ceo && lead != p.Gm && lead.Manager is null && lead.Department!.Type == DepartmentType.HeadOffice) lead.Manager = p.Gm;
        }
        regionManager.Manager = leads[FormCatalog.Depts.SalesSupport];
        foreach (var u in p.All.Where(u => u.Manager is null && u != p.Ceo && u != p.Gm))
            if (u.Department is { } d && leads.TryGetValue(d.Name, out var l) && l != u) u.Manager = l;
        p.SuperAdmin.Manager = p.ItDirector;
        p.Admin.Manager = p.ItDirector;
        p.NewHire.Manager = leads[FormCatalog.Depts.Marketing];

        db.Users.AddRange(p.All);
        await db.SaveChangesAsync(ct);
        return p;
    }

    private DateOnly BirthDate()
    {
        // Doğum günü modülünde görünmesi için birkaç kişinin doğum günü bu haftaya denk getirilir
        var age = _rnd.Next(22, 58);
        var thisWeek = _rnd.NextDouble() < 0.12;
        var day = thisWeek ? DateOnly.FromDateTime(_now.AddDays(_rnd.Next(-1, 6))) : DateOnly.FromDateTime(_now.AddDays(_rnd.Next(0, 365)));
        var year = _now.Year - age;
        return new DateOnly(year, day.Month, Math.Min(day.Day, DateTime.DaysInMonth(year, day.Month)));
    }

    private static string Slug(string s) => s.Replace("İ", "I").ToLowerInvariant().Replace('ı', 'i').Replace('ş', 's').Replace('ğ', 'g').Replace('ü', 'u')
        .Replace('ö', 'o').Replace('ç', 'c').Replace('i', 'i').Replace(" ", "");

    private async Task<Dictionary<string, Group>> SeedGroups(Dictionary<string, Department> depts, People p, CancellationToken ct)
    {
        var it = depts[FormCatalog.Depts.It];
        var groups = new Dictionary<string, Group>
        {
            ["L1"] = new() { Name = "IT Destek L1", Description = "Son kullanıcı donanım/yazılım desteği", Department = it, IsSupportGroup = true },
            ["NET"] = new() { Name = "Ağ ve Altyapı", Description = "Ağ, VPN, sunucu ve güvenlik", Department = it, IsSupportGroup = true },
            ["APP"] = new() { Name = "Kurumsal Uygulamalar", Description = "ERP, CRM, WMS ve iş uygulamaları", Department = it, IsSupportGroup = true },
            ["HR"] = new() { Name = "İK Operasyon", Description = "Özlük, bordro ve izin süreçleri", Department = depts[FormCatalog.Depts.Hr] },
        };
        groups["L1"].Members.Add(new GroupMember { User = p.ItAgents[0], IsLead = false });
        groups["L1"].Members.Add(new GroupMember { User = p.ItAgents[1], IsLead = true });
        groups["NET"].Members.Add(new GroupMember { User = p.ItAgents[2], IsLead = true });
        groups["NET"].Members.Add(new GroupMember { User = p.ItAgents[1] });
        groups["APP"].Members.Add(new GroupMember { User = p.ItAgents[3], IsLead = true });
        groups["APP"].Members.Add(new GroupMember { User = p.Admin });
        groups["HR"].Members.Add(new GroupMember { User = p.HrSpecialist, IsLead = true });
        groups["HR"].Members.Add(new GroupMember { User = p.HrDirector });
        db.Groups.AddRange(groups.Values);
        await db.SaveChangesAsync(ct);
        return groups;
    }

    // =============================================================== DESTEK
    private async Task<List<TicketCategory>> SeedHelpdeskConfig(Dictionary<string, Department> depts, Dictionary<string, Group> g, People p, CancellationToken ct)
    {
        var cats = new List<TicketCategory>
        {
            new() { Name = "Donanım", Description = "Bilgisayar, monitör ve çevre birimi arızaları", Color = "#e11d48", DefaultGroup = g["L1"] },
            new() { Name = "Yazılım", Description = "Uygulama kurulumu, lisans ve hata bildirimleri", Color = "#2563eb", DefaultGroup = g["L1"] },
            new() { Name = "Ağ ve İnternet", Description = "Bağlantı, VPN ve Wi-Fi sorunları", Color = "#059669", DefaultGroup = g["NET"] },
            new() { Name = "E-posta", Description = "Posta kutusu, takvim ve dağıtım listeleri", Color = "#d97706", DefaultGroup = g["L1"] },
            new() { Name = "Erişim ve Yetki", Description = "Hesap, şifre sıfırlama, klasör yetkileri", Color = "#7c3aed", DefaultGroup = g["APP"] },
            new() { Name = "Yazıcı", Description = "Yazıcı, tarayıcı ve toner talepleri", Color = "#0891b2", DefaultGroup = g["L1"] },
            new() { Name = "ERP / İş Uygulamaları", Description = "SAP, CRM, WMS ve mağaza uygulamaları", Color = "#db2777", DefaultGroup = g["APP"] },
            new() { Name = "Mağaza Sistemleri", Description = "Kasa (POS), el terminali, mağaza ağı", Color = "#ea580c", DefaultGroup = g["APP"] },
        };
        db.TicketCategories.AddRange(cats);
        db.RoutingRules.AddRange(
            new RoutingRule { Name = "VPN ve bağlantı talepleri Ağ ekibine", SortOrder = 10, Keyword = "VPN", AssignGroup = g["NET"] },
            new RoutingRule { Name = "Kritik ağ arızaları doğrudan ağ uzmanına", SortOrder = 20, Category = cats[2], MinPriority = TicketPriority.Critical, AssignGroup = g["NET"], AssignUser = p.ItAgents[2] },
            new RoutingRule { Name = "Kasa (POS) arızaları kritik önceliğe", SortOrder = 30, Keyword = "kasa", SetPriority = TicketPriority.Critical, AssignGroup = g["APP"] },
            new RoutingRule { Name = "Mağaza talepleri Kurumsal Uygulamalar ekibine", SortOrder = 40, Category = cats[7], AssignGroup = g["APP"] },
            new RoutingRule { Name = "Muhasebe ERP talepleri uygulama uzmanına", SortOrder = 50, Category = cats[6], Department = depts[FormCatalog.Depts.Accounting], AssignGroup = g["APP"], AssignUser = p.ItAgents[3] });
        await db.SaveChangesAsync(ct);
        return cats;
    }

    private async Task<List<Asset>> SeedInventory(Dictionary<string, Department> depts, People p, CancellationToken ct)
    {
        var models = new (AssetType Type, string Maker, string Model, string Prefix, decimal Cost)[]
        {
            (AssetType.Laptop, "Lenovo", "ThinkPad T14 Gen 5", "LT", 42000), (AssetType.Laptop, "Dell", "Latitude 7450", "LT", 46500),
            (AssetType.Laptop, "Apple", "MacBook Pro 14 M4", "LT", 78000), (AssetType.Computer, "HP", "EliteDesk 800 G9", "PC", 28500),
            (AssetType.Monitor, "Dell", "UltraSharp U2724D", "MN", 11200), (AssetType.Monitor, "LG", "27UP850", "MN", 9800),
        };
        var counter = new Dictionary<string, int>();
        string Tag(string prefix) { counter[prefix] = counter.GetValueOrDefault(prefix) + 1; return $"DP-{prefix}-{counter[prefix]:0000}"; }

        var assets = new List<Asset>();
        foreach (var u in p.Staff.Where(u => u.Department!.Type == DepartmentType.HeadOffice))
        {
            foreach (var m in new[] { models[_rnd.Next(0, 4)], models[_rnd.Next(4, 6)] })
            {
                var purchase = DateOnly.FromDateTime(_now.AddDays(-_rnd.Next(60, 1400)));
                var a = new Asset
                {
                    Name = $"{m.Maker} {m.Model}", AssetTag = Tag(m.Prefix), Type = m.Type, Status = AssetStatus.InUse, Manufacturer = m.Maker, Model = m.Model,
                    SerialNumber = $"{m.Maker[..2].ToUpperInvariant()}{_rnd.Next(100000, 999999)}", Department = u.Department, Location = "Genel Müdürlük",
                    AssignedTo = u, PurchaseDate = purchase, WarrantyEndsAt = purchase.AddYears(m.Type == AssetType.Laptop ? 3 : 2), PurchaseCost = m.Cost,
                    CreatedAt = purchase.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                };
                var assignedAt = a.CreatedAt.AddDays(_rnd.Next(1, 20));
                // Bazı cihazların önceki zimmet geçmişi de olsun
                if (_rnd.NextDouble() < 0.25)
                {
                    var prev = p.Staff[_rnd.Next(p.Staff.Count)];
                    a.Assignments.Add(new AssetAssignment { User = prev, AssignedBy = p.ItAgents[0], AssignedAt = assignedAt, ReturnedAt = assignedAt.AddDays(200), ReturnedBy = p.ItAgents[1], ReturnCondition = "Sağlam, temizlendi", Note = "İlk zimmet" });
                    assignedAt = assignedAt.AddDays(205);
                }
                a.Assignments.Add(new AssetAssignment { User = u, AssignedBy = p.ItAgents[_rnd.Next(2)], AssignedAt = assignedAt, Note = "Zimmet tutanağı imzalandı" });
                assets.Add(a);
            }
        }
        // Mağaza cihazları
        foreach (var store in p.All.Where(u => u.Department!.Type == DepartmentType.Store).Select(u => u.Department!).Distinct())
        {
            assets.Add(new Asset { Name = "Zebra TC52 El Terminali", AssetTag = Tag("HT"), Type = AssetType.HandheldTerminal, Status = AssetStatus.InUse, Manufacturer = "Zebra", Model = "TC52", SerialNumber = $"ZB{_rnd.Next(100000, 999999)}", Department = store, Location = store.Name, PurchaseCost = 24000, PurchaseDate = DateOnly.FromDateTime(_now.AddDays(-_rnd.Next(100, 900))), WarrantyEndsAt = DateOnly.FromDateTime(_now.AddDays(_rnd.Next(-60, 500))) });
            assets.Add(new Asset { Name = "HP Kasa Bilgisayarı (POS)", AssetTag = Tag("POS"), Type = AssetType.Computer, Status = AssetStatus.InUse, Manufacturer = "HP", Model = "Engage One", SerialNumber = $"POS{_rnd.Next(10000, 99999)}", Department = store, Location = store.Name, PurchaseCost = 38000, PurchaseDate = DateOnly.FromDateTime(_now.AddDays(-_rnd.Next(100, 1500))), WarrantyEndsAt = DateOnly.FromDateTime(_now.AddDays(_rnd.Next(-200, 600))) });
        }
        var it = depts[FormCatalog.Depts.It];
        for (var i = 0; i < 8; i++)
        {
            var m = models[_rnd.Next(models.Length)];
            assets.Add(new Asset { Name = $"{m.Maker} {m.Model}", AssetTag = Tag(m.Prefix), Type = m.Type, Status = i < 6 ? AssetStatus.InStock : AssetStatus.InRepair, Manufacturer = m.Maker, Model = m.Model, SerialNumber = $"ST{_rnd.Next(100000, 999999)}", Department = it, Location = "BT Depo", PurchaseCost = m.Cost, PurchaseDate = DateOnly.FromDateTime(_now.AddDays(-_rnd.Next(10, 200))), WarrantyEndsAt = DateOnly.FromDateTime(_now.AddDays(_rnd.Next(20, 700))), Notes = i >= 6 ? "Servise gönderildi, dönüş bekleniyor." : null });
        }
        assets.AddRange(
        [
            new Asset { Name = "Canon imageRUNNER 2630i", AssetTag = "DP-PR-0001", Type = AssetType.Printer, Status = AssetStatus.InUse, Manufacturer = "Canon", Model = "iR 2630i", SerialNumber = "CNX99812", Department = it, Location = "Genel Müdürlük 3. Kat", PurchaseCost = 62000, PurchaseDate = new DateOnly(2024, 1, 9), WarrantyEndsAt = DateOnly.FromDateTime(_now.AddDays(45)) },
            new Asset { Name = "HP LaserJet Pro M404dn", AssetTag = "DP-PR-0002", Type = AssetType.Printer, Status = AssetStatus.InUse, Manufacturer = "HP", Model = "M404dn", SerialNumber = "PHB8K12345", Department = it, Location = "Genel Müdürlük 1. Kat", PurchaseCost = 14500, PurchaseDate = new DateOnly(2023, 3, 14), WarrantyEndsAt = new DateOnly(2026, 3, 14) },
            new Asset { Name = "Cisco Catalyst 9200 Çekirdek Switch", AssetTag = "DP-NW-0001", Type = AssetType.NetworkDevice, Status = AssetStatus.InUse, Manufacturer = "Cisco", Model = "C9200-48P", SerialNumber = "FOC2233X0AB", Department = it, Location = "Veri Merkezi", PurchaseCost = 98000, PurchaseDate = new DateOnly(2023, 6, 1), WarrantyEndsAt = new DateOnly(2028, 6, 1) },
            new Asset { Name = "Fortinet FortiGate 100F", AssetTag = "DP-NW-0002", Type = AssetType.NetworkDevice, Status = AssetStatus.InUse, Manufacturer = "Fortinet", Model = "FG-100F", SerialNumber = "FG100FTK2101", Department = it, Location = "Veri Merkezi", PurchaseCost = 135000, PurchaseDate = new DateOnly(2024, 2, 20), WarrantyEndsAt = DateOnly.FromDateTime(_now.AddDays(70)) },
            new Asset { Name = "Dell PowerEdge R760 (Sanallaştırma)", AssetTag = "DP-SV-0001", Type = AssetType.Server, Status = AssetStatus.InUse, Manufacturer = "Dell", Model = "PowerEdge R760", SerialNumber = "DPER760-55A", Department = it, Location = "Veri Merkezi", PurchaseCost = 420000, PurchaseDate = new DateOnly(2024, 5, 5), WarrantyEndsAt = new DateOnly(2029, 5, 5) },
            new Asset { Name = "Synology RS1221+ Yedekleme", AssetTag = "DP-SV-0002", Type = AssetType.Server, Status = AssetStatus.InUse, Manufacturer = "Synology", Model = "RS1221+", SerialNumber = "SYN1221-901", Department = it, Location = "Veri Merkezi", PurchaseCost = 68000, PurchaseDate = new DateOnly(2023, 11, 11), WarrantyEndsAt = DateOnly.FromDateTime(_now.AddDays(25)) },
            new Asset { Name = "Lenovo ThinkPad T480 (hurda)", AssetTag = "DP-LT-0999", Type = AssetType.Laptop, Status = AssetStatus.Retired, Manufacturer = "Lenovo", Model = "T480", SerialNumber = "PF1OLD99", Department = it, Location = "BT Depo", PurchaseCost = 12000, PurchaseDate = new DateOnly(2019, 4, 1), WarrantyEndsAt = new DateOnly(2022, 4, 1), Notes = "Hurdaya ayrıldı." },
        ]);
        db.Assets.AddRange(assets);

        var licenses = new List<SoftwareLicense>
        {
            new() { Name = "Microsoft 365 E3", Vendor = "Microsoft", Version = "2026", Seats = 150, ExpiresAt = DateOnly.FromDateTime(_now.AddDays(220)), Cost = 1_250_000 },
            new() { Name = "Adobe Creative Cloud", Vendor = "Adobe", Version = "2026", Seats = 8, ExpiresAt = DateOnly.FromDateTime(_now.AddDays(40)), Cost = 210_000 },
            new() { Name = "SAP S/4HANA Kullanıcı", Vendor = "SAP", Version = "2023", Seats = 60, ExpiresAt = DateOnly.FromDateTime(_now.AddDays(400)), Cost = 2_400_000 },
            new() { Name = "FortiClient VPN", Vendor = "Fortinet", Version = "7.4", Seats = 120, ExpiresAt = DateOnly.FromDateTime(_now.AddDays(150)), Cost = 95_000 },
            new() { Name = "Power BI Pro", Vendor = "Microsoft", Version = "2026", Seats = 15, ExpiresAt = DateOnly.FromDateTime(_now.AddDays(-5)), Cost = 72_000, Notes = "Yenileme bekleniyor." },
        };
        db.SoftwareLicenses.AddRange(licenses);
        await db.SaveChangesAsync(ct);

        foreach (var a in assets.Where(a => a.Type is AssetType.Laptop or AssetType.Computer && a.Status == AssetStatus.InUse && a.AssignedTo is not null))
        {
            db.SoftwareInstallations.Add(new SoftwareInstallation { License = licenses[0], Asset = a, InstalledAt = a.CreatedAt.AddDays(2) });
            db.SoftwareInstallations.Add(new SoftwareInstallation { License = licenses[3], Asset = a, InstalledAt = a.CreatedAt.AddDays(2) });
            if (a.AssignedTo!.Department!.Name is "Tasarım Departmanı" or FormCatalog.Depts.Marketing)
                db.SoftwareInstallations.Add(new SoftwareInstallation { License = licenses[1], Asset = a });
            if (a.AssignedTo!.Department!.Name is FormCatalog.Depts.Accounting or "Finans Departmanı" or FormCatalog.Depts.Purchasing)
                db.SoftwareInstallations.Add(new SoftwareInstallation { License = licenses[2], Asset = a });
        }
        await db.SaveChangesAsync(ct);
        return assets;
    }

    private async Task SeedTickets(List<TicketCategory> cats, Dictionary<string, Group> groups, People p, List<Asset> assets, CancellationToken ct)
    {
        var templates = new (int Cat, string Title, string Desc, TicketPriority P, TicketType T, AssetType? Asset)[]
        {
            (0, "Dizüstü bilgisayar açılmıyor", "Sabah bilgisayarımı açmaya çalıştığımda güç ışığı yanıyor ama ekran gelmiyor. Şarj aleti takılı.", TicketPriority.High, TicketType.Incident, AssetType.Laptop),
            (0, "Monitörde titreme var", "İkinci monitörüm birkaç dakikada bir titriyor ve kararıyor. Kabloyu değiştirmeyi denedim.", TicketPriority.Medium, TicketType.Incident, AssetType.Monitor),
            (0, "Klavyenin bazı tuşları çalışmıyor", "Ç, Ş ve Ğ tuşları basıldığında karakter yazmıyor.", TicketPriority.Low, TicketType.Incident, null),
            (1, "Office lisansı etkinleştirilemiyor", "Excel açıldığında 'Ürün etkinleştirilemedi' uyarısı çıkıyor ve dosya kaydedemiyorum.", TicketPriority.High, TicketType.Incident, null),
            (1, "Adobe Acrobat kurulumu", "Sözleşme düzenlemeleri için Acrobat Pro kurulumuna ihtiyacım var.", TicketPriority.Low, TicketType.Request, null),
            (1, "Teams toplantılarında ses gitmiyor", "Toplantıya katıldığımda karşı taraf beni duyamıyor, mikrofon ayarları doğru görünüyor.", TicketPriority.Medium, TicketType.Incident, AssetType.Laptop),
            (2, "VPN bağlantısı sürekli kopuyor", "Evden çalışırken VPN 10-15 dakikada bir kopuyor, ERP oturumum düşüyor.", TicketPriority.High, TicketType.Incident, null),
            (2, "Toplantı odasında Wi-Fi çekmiyor", "3. kat büyük toplantı odasında kablosuz ağ sinyali çok zayıf.", TicketPriority.Medium, TicketType.Incident, null),
            (3, "Posta kutusu dolu uyarısı", "Outlook posta kutumun dolduğunu söylüyor, e-posta gönderemiyorum.", TicketPriority.Medium, TicketType.Incident, null),
            (3, "Şüpheli e-posta aldım", "Bankadan geliyormuş gibi görünen ve şifre isteyen bir e-posta aldım, linke tıklamadım.", TicketPriority.Critical, TicketType.Incident, null),
            (4, "Ortak klasöre erişim yetkisi", "Finans ortak klasörüne okuma yetkisi talep ediyorum. Yöneticim onayladı.", TicketPriority.Medium, TicketType.Request, null),
            (4, "Hesabım kilitlendi", "Windows oturum şifremi birkaç kez hatalı girdim, hesabım kilitlendi.", TicketPriority.High, TicketType.Request, null),
            (5, "Yazıcı kâğıt sıkıştırıyor", "3. kattaki yazıcı her çıktıda kâğıt sıkıştırıyor.", TicketPriority.Medium, TicketType.Incident, AssetType.Printer),
            (5, "Tarayıcı e-postaya gönderemiyor", "Çok fonksiyonlu cihazdan taradığım belgeler e-postama gelmiyor.", TicketPriority.Medium, TicketType.Incident, AssetType.Printer),
            (6, "SAP'de fatura kaydı hata veriyor", "MIRO ekranında kaydet dediğimde 'Dönem kapalı' hatası alıyorum.", TicketPriority.High, TicketType.Incident, null),
            (6, "CRM raporu güncellenmiyor", "Sadakat üyeleri raporu dünden beri güncellenmiyor.", TicketPriority.Medium, TicketType.Incident, null),
            (7, "Mağaza kasası açılmıyor", "Kasa bilgisayarı açılışta mavi ekran veriyor, satış yapamıyoruz.", TicketPriority.Critical, TicketType.Incident, AssetType.Computer),
            (7, "El terminali WMS'e bağlanmıyor", "Sayım yaparken el terminali sürekli 'sunucuya ulaşılamıyor' hatası veriyor.", TicketPriority.High, TicketType.Incident, AssetType.HandheldTerminal),
        };
        var replies = new[]
        {
            "Merhaba, talebiniz incelemeye alınmıştır. En kısa sürede dönüş yapacağız.",
            "Uzak bağlantı ile kontrol ettim; sorun sürücü güncellemesi sonrası çözüldü. Tekrar yaşarsanız lütfen bildiriniz.",
            "Cihazı yerinde kontrol etmek için yarın saat 10:00'da yanınıza geleceğim.",
            "Gerekli yetki tanımlandı. Oturumu kapatıp açtıktan sonra erişebilirsiniz.",
            "Parça siparişi verildi, tedarik süresi 3 iş günüdür. Bu süre için yedek cihaz teslim edildi.",
        };
        var userReplies = new[] { "Teşekkür ederim, şu an sorunsuz çalışıyor.", "Hâlâ aynı sorunu yaşıyorum, tekrar bakabilir misiniz?", "Ek bilgi: Sorun özellikle öğleden sonra artıyor.", "Yarın 10:00 benim için uygundur." };
        var internalNotes = new[] { "Aynı modelde ikinci arıza, garanti kapsamında RMA açılmalı.", "Kullanıcının hesabında MFA eksik, ayrıca hatırlatılacak.", "Switch portunda CRC hataları görüldü, kablo değişimi planlandı." };

        var requesters = p.Staff.Where(u => !u.Roles.Any(r => r.Role.Code == RoleCodes.It)).ToList();
        requesters.AddRange(Enumerable.Repeat(p.Employee, 6)); // demo çalışanının talepleri görünür olsun
        var tickets = new List<Ticket>();
        for (var i = 0; i < 140; i++)
        {
            var tpl = templates[_rnd.Next(templates.Length)];
            var requester = requesters[_rnd.Next(requesters.Count)];
            if (tpl.Cat == 7 && requester.Department!.Type != DepartmentType.Store)
                requester = p.All.Where(u => u.Department!.Type == DepartmentType.Store).ElementAt(_rnd.Next(p.All.Count(u => u.Department!.Type == DepartmentType.Store)));
            var ageHours = Math.Pow(_rnd.NextDouble(), 1.5) * 60 * 24;
            var created = _now.AddHours(-ageHours);
            var priority = _rnd.NextDouble() < 0.7 ? tpl.P : (TicketPriority)_rnd.Next(0, 4);
            var cat = cats[tpl.Cat];
            var t = new Ticket
            {
                Title = tpl.Title, Description = tpl.Desc, Category = cat, Priority = priority, Type = tpl.T, Requester = requester,
                Department = requester.Department, Group = cat.DefaultGroup, CreatedAt = created, UpdatedAt = created, DueAt = created + TicketRules.SlaFor(priority),
            };
            if (tpl.Asset is { } at)
                t.Asset = assets.FirstOrDefault(a => a.AssignedTo == requester && a.Type == at) ?? assets.FirstOrDefault(a => a.Department == requester.Department && a.Type == at);
            t.History.Add(new TicketHistory { Field = "created", NewValue = "Açık", Actor = requester, CreatedAt = created });

            var progress = ageHours / (60 * 24);
            var roll = _rnd.NextDouble();
            var status = roll < 0.12 + progress * 0.05 ? TicketStatus.Open
                : roll < 0.30 ? TicketStatus.InProgress
                : roll < 0.37 ? TicketStatus.Pending
                : roll < 0.37 + 0.2 + progress * 0.25 ? TicketStatus.Resolved
                : TicketStatus.Closed;
            if (ageHours < 6 && status != TicketStatus.Open) status = _rnd.NextDouble() < 0.5 ? TicketStatus.Open : TicketStatus.InProgress;

            var cursor = created;
            DateTime Step(double maxHours)
            {
                cursor = cursor.AddMinutes(_rnd.Next(10, (int)Math.Max(20, maxHours * 60)));
                if (cursor > _now) cursor = _now.AddMinutes(-_rnd.Next(1, 30));
                return cursor;
            }

            if (status != TicketStatus.Open || _rnd.NextDouble() < 0.4)
            {
                var pool = t.Group?.Members.Select(m => m.User).ToList() is { Count: > 0 } members ? members : p.ItAgents;
                var agent = pool[_rnd.Next(pool.Count)];
                t.Assignee = agent;
                t.History.Add(new TicketHistory { Field = "assignee", NewValue = agent.FullName, Actor = agent, CreatedAt = Step(2) });
                if (status != TicketStatus.Open)
                {
                    t.History.Add(new TicketHistory { Field = "status", OldValue = "Açık", NewValue = "İşlemde", Actor = agent, CreatedAt = cursor });
                    t.Comments.Add(new TicketComment { Author = agent, Body = replies[0], CreatedAt = Step(1) });
                }
                if (_rnd.NextDouble() < 0.45) t.Comments.Add(new TicketComment { Author = requester, Body = userReplies[_rnd.Next(userReplies.Length)], CreatedAt = Step(6) });
                if (_rnd.NextDouble() < 0.3) t.Comments.Add(new TicketComment { Author = agent, Body = internalNotes[_rnd.Next(internalNotes.Length)], IsInternal = true, CreatedAt = Step(3) });
                if (status == TicketStatus.Pending)
                    t.History.Add(new TicketHistory { Field = "status", OldValue = "İşlemde", NewValue = "Beklemede", Actor = agent, CreatedAt = Step(4) });
                if (status is TicketStatus.Resolved or TicketStatus.Closed)
                {
                    var target = TicketRules.SlaFor(priority).TotalHours * (_rnd.NextDouble() < 0.82 ? _rnd.NextDouble() * 0.9 : 1.1 + _rnd.NextDouble());
                    var resolvedAt = created.AddHours(target);
                    if (resolvedAt <= cursor) resolvedAt = cursor.AddMinutes(_rnd.Next(5, 90));
                    if (resolvedAt > _now) resolvedAt = _now.AddMinutes(-_rnd.Next(5, 60));
                    cursor = resolvedAt;
                    t.Comments.Add(new TicketComment { Author = agent, Body = replies[_rnd.Next(1, replies.Length)], CreatedAt = resolvedAt.AddMinutes(-2) });
                    t.History.Add(new TicketHistory { Field = "status", OldValue = "İşlemde", NewValue = "Çözüldü", Actor = agent, CreatedAt = resolvedAt });
                    t.ResolvedAt = resolvedAt;
                    if (status == TicketStatus.Closed)
                    {
                        var closedAt = resolvedAt.AddHours(_rnd.Next(1, 30));
                        if (closedAt > _now) closedAt = _now;
                        t.ClosedAt = closedAt;
                        cursor = closedAt;
                        t.History.Add(new TicketHistory { Field = "status", OldValue = "Çözüldü", NewValue = "Kapandı", Actor = requester, CreatedAt = closedAt });
                    }
                }
            }
            t.Status = status;
            t.UpdatedAt = cursor;
            tickets.Add(t);
        }
        db.Tickets.AddRange(tickets);
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedKnowledge(List<TicketCategory> cats, People p, CancellationToken ct)
    {
        KnowledgeArticle A(string title, string summary, int cat, User author, int views, int helpful, string content, bool faq = false) => new()
        {
            Title = title, Summary = summary, Category = cats[cat], Author = author, Views = views, HelpfulCount = helpful, Content = content, IsFaq = faq,
            CreatedAt = _now.AddDays(-_rnd.Next(20, 200)), UpdatedAt = _now.AddDays(-_rnd.Next(1, 20)),
        };
        db.KnowledgeArticles.AddRange(
            A("VPN bağlantısı nasıl kurulur?", "FortiClient ile şirket ağına uzaktan güvenli bağlantı adımları.", 2, p.ItAgents[2], 412, 58,
                "## Gereksinimler\n- Şirket tarafından verilen dizüstü bilgisayar\n- Aktif kullanıcı hesabı ve MFA\n\n## Adımlar\n1. Başlat menüsünden **FortiClient VPN** uygulamasını açın.\n2. Bağlantı adı olarak `Devoplus-VPN` seçin.\n3. Kullanıcı adınızı ve şifrenizi girin.\n4. Telefonunuza gelen MFA bildirimini onaylayın.\n\n## Sık karşılaşılan sorunlar\n- **Bağlantı %40'ta takılıyor:** İnternet bağlantınızı kontrol edin, ardından uygulamayı yeniden başlatın.\n- **Sık kopma:** Wi-Fi güç tasarrufu modunu kapatın."),
            A("Şifre sıfırlama ve hesap kilidi", "Hesap kilitlendiğinde ve şifre unutulduğunda izlenecek yol.", 4, p.ItAgents[0], 655, 91,
                "## Hesap kilitlendi\nArt arda hatalı denemelerden sonra hesabınız geçici olarak kilitlenir. **Erişim ve Yetki** kategorisinde talep açarak kilidin kaldırılmasını isteyebilirsiniz.\n\n## Güçlü şifre kuralları\n- En az **10 karakter**\n- Büyük harf, küçük harf ve rakam\n- Son 5 şifreden farklı"),
            A("Outlook posta kutusu doldu", "Arşivleme ve büyük ekleri temizleme yöntemleri.", 3, p.ItAgents[1], 238, 33,
                "1. Outlook'ta **Dosya > Araçlar > Posta Kutusunu Temizle** adımını izleyin.\n2. 5 MB üzerindeki ekleri içeren e-postaları OneDrive'a taşıyın.\n3. *Silinmiş Öğeler* klasörünü boşaltın."),
            A("Şüpheli e-posta (oltalama) nasıl anlaşılır?", "Phishing e-postalarını tanıma ve bildirme rehberi.", 3, p.ItAgents[2], 301, 77,
                "## Uyarı işaretleri\n- Gönderen adresi kurum adını taklit ediyor\n- Acele ettiren ifadeler\n- Şifre veya kart bilgisi isteyen formlar\n\n## Ne yapmalı?\n1. Bağlantılara tıklamayın.\n2. **Kimlik Avı Bildir** butonunu kullanın.\n3. Yanlışlıkla tıkladıysanız şifrenizi değiştirip **Kritik** öncelikli talep açın."),
            A("Mağaza kasası (POS) açılmıyorsa", "Kasa arızalarında mağaza ekibinin uygulayacağı ilk adımlar.", 7, p.ItAgents[3], 189, 41,
                "1. Kasayı tamamen kapatıp 30 saniye bekleyin.\n2. Ağ kablosunun takılı olduğundan emin olun.\n3. Açılmazsa yedek kasaya geçin ve **Kritik** öncelikli talep açın; talebe mağaza kodunu yazın."),
            A("Talep açarken nelere dikkat etmeliyim?", "Hızlı çözüm için talepte bulunması gereken bilgiler.", 1, p.Admin, 520, 88,
                "- Sorunu tek cümleyle özetleyen bir başlık\n- Hata mesajının birebir metni veya ekran görüntüsü\n- Sorunun ne zamandan beri yaşandığı\n- Etkilenen cihazın envanter numarası", true),
            A("İzin bakiyemi nereden görebilirim?", "Kalan yıllık izin günlerinizi görüntüleme.", 4, p.HrSpecialist, 410, 70,
                "Portal ana sayfasındaki **İzin Bakiyesi** kartında ve **İzin** uygulamasında kalan gün sayınız görünür. Bakiye; kıdeminize göre hesaplanan hak ediş, devreden günler ve onaylanan izinlerinizden otomatik hesaplanır.", true),
            A("Avans ve masraf limitleri nelerdir?", "Muhasebe onay limitleri hakkında.", 6, p.Manager, 150, 22,
                "Güncel limitler **Muhasebe → Limitler** ekranında yayımlanır. Limit üzerindeki talepler sistem tarafından otomatik olarak reddedilmez; ancak gönderim sırasında uyarı verilir ve gerekçe istenir.", true),
            A("Şirket dışından portala erişebilir miyim?", "Uzaktan erişim kuralları.", 2, p.ItAgents[2], 98, 15,
                "Portala yalnızca şirket ağından veya **VPN** üzerinden erişilebilir. Kişisel cihazlardan erişim BT onayına tabidir.", true));
        await db.SaveChangesAsync(ct);
    }

    // =============================================================== FORMLAR
    private async Task<List<FormDefinition>> SeedForms(Dictionary<string, Department> depts, CancellationToken ct)
    {
        var order = 0;
        var forms = FormCatalog.All.Select(d => new FormDefinition
        {
            Key = d.Key, Name = d.Name, Module = d.Module, Description = d.Description, Icon = d.Icon, Color = d.Color, Kind = d.Kind,
            OwnerDepartment = depts.GetValueOrDefault(d.Owner), Fields = d.Fields, Steps = d.Steps, SortOrder = ++order,
        }).ToList();
        db.FormDefinitions.AddRange(forms);
        db.SpendingLimits.AddRange(
            new SpendingLimit { Kind = FormKinds.Advance, Label = "İş avansı üst limiti", MaxAmount = 25_000 },
            new SpendingLimit { Kind = FormKinds.Expense, Label = "Tek masraf belgesi üst limiti", MaxAmount = 15_000 });
        await db.SaveChangesAsync(ct);
        return forms;
    }

    private async Task SeedWorkflowData(List<FormDefinition> forms, People p, CancellationToken ct)
    {
        // İzin bakiyeleri (kıdeme göre)
        var today = DateOnly.FromDateTime(_now);
        foreach (var u in p.All)
        {
            var ent = LeaveRules.AnnualEntitlement(u.HireDate, today);
            db.LeaveBalances.Add(new LeaveBalance { User = u, Year = _now.Year, Entitled = ent, CarriedOver = ent == 0 ? 0 : _rnd.Next(0, 6), Used = ent == 0 ? 0 : _rnd.Next(0, Math.Max(1, ent - 4)) });
        }

        var byKey = forms.ToDictionary(f => f.Key);
        var seq = 0;
        FormSubmission Sub(string key, User submitter, Dictionary<string, object?> data, SubmissionStatus status, int daysAgo, string? summary = null, decimal? amount = null)
        {
            var form = byKey[key];
            var created = _now.AddDays(-daysAgo).AddHours(-_rnd.Next(0, 8));
            var s = new FormSubmission
            {
                Number = $"FRM-{created.Year}-{++seq:000000}", FormDefinition = form, Submitter = submitter, Department = submitter.Department,
                Data = JsonSerializer.Serialize(data, Json), Status = status, Summary = summary, Amount = amount, CreatedAt = created, UpdatedAt = created,
            };
            s.Actions.Add(new SubmissionAction { Action = SubmissionActionType.Submitted, Actor = submitter, CreatedAt = created, Comment = "Başvuru oluşturuldu." });
            var approvers = new List<User?> { submitter.Manager, form.OwnerDepartment?.Manager ?? p.Admin };
            var t = created;
            var finalStep = status == SubmissionStatus.Pending ? _rnd.Next(0, form.Steps.Count) : form.Steps.Count;
            for (var i = 0; i < form.Steps.Count && i < finalStep; i++)
            {
                t = t.AddHours(_rnd.Next(2, 30));
                var approver = (form.Steps[i].ApproverType == ApproverType.Manager ? submitter.Manager : form.OwnerDepartment?.Manager) ?? p.Admin;
                if (status == SubmissionStatus.Rejected && i == form.Steps.Count - 1)
                {
                    s.Actions.Add(new SubmissionAction { Action = SubmissionActionType.Rejected, Actor = approver, StepIndex = i, StepName = form.Steps[i].Name, Comment = "Bütçe bu dönem için uygun değil, bir sonraki çeyrekte tekrar değerlendirilecek.", CreatedAt = t });
                    break;
                }
                s.Actions.Add(new SubmissionAction { Action = SubmissionActionType.Approved, Actor = approver, StepIndex = i, StepName = form.Steps[i].Name, Comment = "Uygundur.", CreatedAt = t });
            }
            s.CurrentStep = status == SubmissionStatus.Pending ? finalStep : form.Steps.Count;
            if (status == SubmissionStatus.Pending)
            {
                var step = form.Steps[s.CurrentStep];
                var approver = step.ApproverType switch
                {
                    ApproverType.Manager => submitter.Manager,
                    ApproverType.Role when step.RoleCode == RoleCodes.Hr => p.HrSpecialist,
                    ApproverType.Role => p.Gm,
                    _ => form.OwnerDepartment?.Manager,
                } ?? p.Admin;
                s.PendingApproverIds = [approver.Id];
            }
            if (status == SubmissionStatus.Completed)
            {
                t = t.AddHours(_rnd.Next(2, 48));
                s.Actions.Add(new SubmissionAction { Action = SubmissionActionType.Completed, Actor = form.OwnerDepartment?.Manager ?? p.Admin, Comment = "İşlem tamamlandı.", CreatedAt = t });
                s.CompletedAt = t;
            }
            s.UpdatedAt = t;
            return s;
        }

        var e = p.Employee;
        var staff = p.Staff.Where(u => u.Manager is not null).ToList();
        User Any() => staff[_rnd.Next(staff.Count)];
        string D(int days) => DateOnly.FromDateTime(_now.AddDays(days)).ToString("yyyy-MM-dd");
        var annualDays = LeaveRules.WorkingDays(DateOnly.Parse(D(10)), DateOnly.Parse(D(14)));
        string WorkDay(int days) { var d = DateOnly.FromDateTime(_now.AddDays(days)); while (LeaveRules.WorkingDays(d, d) == 0) d = d.AddDays(-1); return d.ToString("yyyy-MM-dd"); }

        db.FormSubmissions.AddRange(
            Sub("leave", e, new() { ["leaveType"] = "Yıllık İzin", ["startDate"] = D(10), ["endDate"] = D(14), ["description"] = "Aile ziyareti", ["days"] = annualDays }, SubmissionStatus.Pending, 1, $"Yıllık İzin · {annualDays} iş günü"),
            Sub("leave", e, new() { ["leaveType"] = "Mazeret İzni", ["startDate"] = WorkDay(-30), ["endDate"] = WorkDay(-30), ["days"] = 1 }, SubmissionStatus.Approved, 33, "Mazeret İzni · 1 iş günü"),
            Sub("hardware-request", e, new() { ["deviceType"] = "Monitör", ["quantity"] = 1, ["justification"] = "Çift ekran ile mutabakat çalışmaları hızlanacak." }, SubmissionStatus.Pending, 2, "Monitör × 1"),
            Sub("expense", e, new() { ["expenseDate"] = D(-6), ["category"] = "Yol", ["amount"] = 1850.50m, ["description"] = "Ankara mağaza ziyareti taksi ve otopark" }, SubmissionStatus.Approved, 5, "Yol · ₺1.850,50", 1850.50m),
            Sub("access-request", e, new() { ["system"] = "Power BI", ["accessLevel"] = "Okuma", ["reason"] = "Aylık ciro raporlarını görüntülemek için." }, SubmissionStatus.Completed, 12, "Power BI · Okuma"),
            Sub("advance", Any(), new() { ["amount"] = 12000, ["repaymentPlan"] = "2 Taksit", ["reason"] = "Fuar katılımı konaklama ve ulaşım." }, SubmissionStatus.Pending, 1, "₺12.000", 12000),
            Sub("travel", Any(), new() { ["destination"] = "İzmir", ["departureDate"] = D(7), ["returnDate"] = D(9), ["transport"] = "Uçak", ["accommodation"] = true, ["purpose"] = "Mağaza açılışı" }, SubmissionStatus.Pending, 2, "İzmir · Uçak"),
            Sub("training", Any(), new() { ["trainingName"] = "Power BI ile İleri Raporlama", ["trainingType"] = "Online", ["cost"] = 7500, ["justification"] = "Departman raporlarının otomasyonu." }, SubmissionStatus.Approved, 20, "Power BI ile İleri Raporlama", 7500),
            Sub("suggestion", Any(), new() { ["title"] = "Mağaza sayımlarında RFID", ["area"] = "Verimlilik", ["proposal"] = "Sayım süresini %70 kısaltmak için RFID etiket pilotu." }, SubmissionStatus.Pending, 4, "Mağaza sayımlarında RFID"),
            Sub("near-miss", Any(), new() { ["location"] = "İzmir Depo 2. koridor", ["eventDate"] = D(-3), ["category"] = "Depolama / İstif", ["severity"] = "Yüksek", ["description"] = "Üst raftaki koliler sabitlenmemişti, transpalet çarpınca kayıyordu." }, SubmissionStatus.Approved, 3, "Depolama / İstif · Yüksek"),
            Sub("work-order", Any(), new() { ["workType"] = "Bakım-Onarım", ["location"] = p.StoreManager.Department!.Name, ["urgency"] = "Yüksek", ["amount"] = 18000, ["description"] = "Mağaza girişindeki otomatik kapı arızalı." }, SubmissionStatus.Rejected, 9, $"Bakım-Onarım · {p.StoreManager.Department!.Name}", 18000),
            Sub("project-request", Any(), new() { ["projectName"] = "Mağaza Görev Uygulaması", ["requestType"] = "Yeni Yazılım / Modül", ["problem"] = "Mağaza görevleri WhatsApp gruplarından takip ediliyor." }, SubmissionStatus.Pending, 6, "Mağaza Görev Uygulaması"),
            Sub("communication", Any(), new() { ["campaignName"] = "Yılbaşı Kampanyası", ["channel"] = new[] { "SMS", "E-posta" }, ["sendDate"] = D(20), ["content"] = "Yeni sezon ürünlerinde %30 indirim." }, SubmissionStatus.Pending, 1, "Yılbaşı Kampanyası"),
            Sub("store-revenue", p.StoreManager, new() { ["storeName"] = p.StoreManager.Department!.Name, ["date"] = D(-1), ["cashAmount"] = 48250, ["cardAmount"] = 186400, ["amount"] = 234650, ["receiptCount"] = 412 }, SubmissionStatus.Completed, 1, $"{p.StoreManager.Department!.Name} · ₺234.650", 234650),
            Sub("shift", p.All.First(u => u.Manager == p.StoreManager), new() { ["unit"] = p.StoreManager.Department!.Name, ["date"] = D(3), ["currentShift"] = "Sabah (09:00-17:00)", ["requestedShift"] = "Akşam (14:00-22:00)", ["reason"] = "Okul sınavı" }, SubmissionStatus.Pending, 1, "Sabah → Akşam"),
            Sub("invoice", p.Manager, new() { ["supplier"] = "ABC Bilişim A.Ş.", ["invoiceNo"] = "ABC2026000187", ["invoiceDate"] = D(-4), ["amount"] = 184000 }, SubmissionStatus.Pending, 3, "ABC Bilişim A.Ş. · ₺184.000", 184000),
            Sub("gift-card", Any(), new() { ["company"] = "Örnek Holding", ["cardCount"] = 150, ["cardValue"] = 500, ["invoiceInfo"] = "Vergi No: 1234567890" }, SubmissionStatus.Completed, 15, "Örnek Holding · 150 kart"),
            Sub("vehicle", Any(), new() { ["purpose"] = "Bölge mağaza ziyaretleri", ["startDate"] = D(2), ["endDate"] = D(4), ["destination"] = "Bursa", ["passengerCount"] = 2 }, SubmissionStatus.Approved, 2, "Bursa · 2 kişi"));
        await db.SaveChangesAsync(ct);
    }

    // =============================================================== İÇERİK
    private async Task SeedContent(Dictionary<string, Department> depts, People p, CancellationToken ct)
    {
        db.Banners.AddRange(
            new Banner { Title = "Dijital dönüşümde yeni dönem: StajDesk", Subtitle = "Tüm talep, onay ve iç iletişim süreçlerimiz artık tek portalda.", Theme = "crimson", LinkUrl = "/login", LinkText = "Portala Git", SortOrder = 1 },
            new Banner { Title = "2026 Kış Sezonu mağazalarda", Subtitle = "242 mağazamızda yeni koleksiyon raflarda.", Theme = "midnight", LinkUrl = "#haberler", LinkText = "Haberleri Oku", SortOrder = 2 },
            new Banner { Title = "Ekibimize katılın", Subtitle = "Merkez ofis ve mağazalarımızda açık pozisyonları inceleyin.", Theme = "sunset", LinkUrl = "#kariyer", LinkText = "Açık Pozisyonlar", SortOrder = 3 },
            new Banner { Title = "Eski kampanya (süresi doldu)", Subtitle = "Bu banner yayın tarihi geçtiği için sitede görünmez.", Theme = "ocean", SortOrder = 4, EndsAt = _now.AddDays(-10) });

        Announcement Ann(string title, string summary, string body, AnnouncementKind kind, bool isPublic, int daysAgo, User author, bool pinned = false) =>
            new() { Title = title, Summary = summary, Body = body, Kind = kind, IsPublic = isPublic, IsPinned = pinned, PublishAt = _now.AddDays(-daysAgo), CreatedAt = _now.AddDays(-daysAgo), Author = author };
        db.Announcements.AddRange(
            Ann("StajDesk portalı kullanıma açıldı", "Talep, izin, masraf ve tüm departman uygulamaları artık tek ekranda.", "Değerli çalışma arkadaşlarımız,\n\nBT destek talepleri, izin ve masraf başvuruları ile departman uygulamalarının tamamı **StajDesk** portalına taşındı. Eski e-posta ve form süreçleri 1 Aralık itibarıyla kullanımdan kalkacaktır.\n\nSorularınız için Bilgi Bankası'nı inceleyebilir veya IT Destek'e talep açabilirsiniz.", AnnouncementKind.Announcement, false, 2, p.ItDirector, true),
            Ann("Yılsonu sayım takvimi", "Tüm mağazalarda yılsonu sayımı 28-30 Aralık tarihlerinde yapılacaktır.", "Sayım günlerinde mağaza vardiyaları İK tarafından ayrıca planlanacaktır. El terminallerinin şarjlı ve güncel olduğundan emin olunuz.", AnnouncementKind.Announcement, false, 5, p.HrDirector),
            Ann("Bilgi güvenliği farkındalık eğitimi zorunludur", "Tüm çalışanların 15 Aralık'a kadar eğitimi tamamlaması gerekmektedir.", "Oltalama (phishing) saldırılarına karşı hazırlanan 20 dakikalık eğitim Eğitim uygulamasında yer almaktadır.", AnnouncementKind.Announcement, false, 8, p.ItDirector),
            Ann("Devoplus, perakende dijital dönüşüm ödülüne layık görüldü", "Mağaza operasyonlarında dijitalleşme projemiz sektör ödülü aldı.", "Mağazalarımızda el terminali, dijital görev yönetimi ve merkezi talep sistemi ile sağladığımız verimlilik artışı ödüle değer görüldü.", AnnouncementKind.News, true, 4, p.Gm),
            Ann("Yeni mağazamız Antalya'da açıldı", "Antalya'daki yeni mağazamız müşterilerini karşılamaya başladı.", "850 m² alana sahip yeni konsept mağazamızda kadın, erkek ve aksesuar koleksiyonları bir arada sunuluyor.", AnnouncementKind.News, true, 10, p.Gm),
            Ann("Sürdürülebilir ambalaja geçiyoruz", "Tüm mağazalarda geri dönüştürülmüş kâğıt poşet kullanımına geçildi.", "Plastik kullanımını yıllık 40 ton azaltmayı hedefliyoruz.", AnnouncementKind.News, true, 18, p.Ceo),
            Ann("Kurum içi mentorluk programı başvuruları açıldı", "Mentor ve menti başvuruları 30 Kasım'a kadar alınacaktır.", "Program 6 ay sürecek olup aylık birebir görüşmeler içermektedir.", AnnouncementKind.News, false, 1, p.HrSpecialist));

        db.Events.AddRange(
            new CompanyEvent { Title = "Çeyrek Değerlendirme Toplantısı", Description = "Q4 hedefleri ve sonuçları.", Location = "Genel Müdürlük Konferans Salonu", Category = "Kurumsal", StartsAt = _now.Date.AddDays(3).AddHours(7), EndsAt = _now.Date.AddDays(3).AddHours(9), CreatedBy = p.Gm },
            new CompanyEvent { Title = "Yılbaşı Kutlaması", Description = "Tüm merkez ofis çalışanlarımız davetlidir.", Location = "Teras Kat", Category = "Sosyal", StartsAt = _now.Date.AddDays(12).AddHours(15), EndsAt = _now.Date.AddDays(12).AddHours(19), CreatedBy = p.HrDirector, IsPublic = false },
            new CompanyEvent { Title = "Bilgi Güvenliği Webinarı", Description = "Oltalama saldırıları ve korunma yolları.", Location = "Microsoft Teams", Category = "Eğitim", StartsAt = _now.Date.AddDays(1).AddHours(11), EndsAt = _now.Date.AddDays(1).AddHours(12), CreatedBy = p.ItDirector },
            new CompanyEvent { Title = "Kariyer Günleri - ODTÜ", Description = "Üniversite kariyer fuarında standımızı ziyaret edin.", Location = "ODTÜ Kültür ve Kongre Merkezi", Category = "Kariyer", StartsAt = _now.Date.AddDays(9).AddHours(7), EndsAt = _now.Date.AddDays(9).AddHours(14), CreatedBy = p.HrSpecialist, IsPublic = true },
            new CompanyEvent { Title = "Kan Bağışı Kampanyası", Description = "Kızılay iş birliğiyle.", Location = "Genel Müdürlük Otopark", Category = "Sosyal Sorumluluk", StartsAt = _now.Date.AddDays(6).AddHours(6), EndsAt = _now.Date.AddDays(6).AddHours(13), CreatedBy = p.HrSpecialist, IsPublic = true });

        db.JobPostings.AddRange(
            new JobPosting { Title = "Yazılım ve Proje Geliştirme Uzmanı (.NET)", Department = depts[FormCatalog.Depts.It], Location = "İstanbul (Hibrit)", EmploymentType = EmploymentType.FullTime, Description = "Kurum içi uygulamalarımızın geliştirilmesinde görev alacak takım arkadaşı arıyoruz.", Requirements = "- C#, ASP.NET Core, EF Core\n- PostgreSQL veya SQL Server\n- React deneyimi tercih sebebidir", ClosesAt = DateOnly.FromDateTime(_now.AddDays(30)) },
            new JobPosting { Title = "Mağaza Yöneticisi", Department = depts[FormCatalog.Depts.SalesSupport], Location = "Ankara", EmploymentType = EmploymentType.FullTime, Description = "Ankara mağazalarımız için deneyimli mağaza yöneticisi.", Requirements = "- Perakende sektöründe en az 3 yıl yöneticilik\n- Ekip yönetimi ve satış hedefi deneyimi", ClosesAt = DateOnly.FromDateTime(_now.AddDays(20)) },
            new JobPosting { Title = "Dönemsel Satış Danışmanı", Location = "Türkiye Geneli", EmploymentType = EmploymentType.PartTime, Description = "Yılsonu sezonu için mağazalarımızda dönemsel satış danışmanları.", Requirements = "- Müşteri odaklılık\n- Esnek çalışma saatlerine uyum", ClosesAt = DateOnly.FromDateTime(_now.AddDays(15)) },
            new JobPosting { Title = "Stajyer - İnsan Kaynakları", Department = depts[FormCatalog.Depts.Hr], Location = "İstanbul", EmploymentType = EmploymentType.Intern, Description = "Üniversite 3. ve 4. sınıf öğrencileri için zorunlu/gönüllü staj.", Requirements = "- İK, işletme veya psikoloji bölümü öğrencisi" },
            new JobPosting { Title = "CRM Kıdemli Uzmanı", Department = depts[FormCatalog.Depts.Marketing], Location = "İstanbul", EmploymentType = EmploymentType.FullTime, Description = "Sadakat programı ve müşteri segmentasyonu.", Requirements = "- SQL ve veri analizi", IsPublic = false });

        var kudosGivers = new[] { p.ItDirector, p.HrDirector, p.Gm, p.Manager, p.StoreManager };
        var titles = new[] { ("Yılın Mağazası", AchievementKind.Award), ("Müşteri memnuniyeti rekoru", AchievementKind.Award), ("Harika ekip çalışması için teşekkürler!", AchievementKind.Kudos), ("10. yıl kıdem", AchievementKind.Milestone), ("Hızlı çözüm için teşekkürler", AchievementKind.Kudos) };
        for (var i = 0; i < 9; i++)
        {
            var (t, k) = titles[_rnd.Next(titles.Length)];
            var giver = kudosGivers[_rnd.Next(kudosGivers.Length)];
            var receiver = p.Staff[_rnd.Next(p.Staff.Count)];
            if (receiver == giver) continue;
            db.Achievements.Add(new Achievement { User = receiver, GivenBy = giver, Title = t, Kind = k, Likes = _rnd.Next(0, 40), Description = k == AchievementKind.Kudos ? "Yoğun dönemde gösterdiğin özveri için teşekkür ederiz." : null, CreatedAt = _now.AddDays(-_rnd.Next(0, 25)) });
        }

        var survey = new Survey
        {
            Title = "Çalışan Memnuniyeti Anketi 2026", Description = "Görüşleriniz anonim olarak toplanmaktadır.", IsAnonymous = true, EndsAt = _now.AddDays(14), CreatedBy = p.HrDirector,
            Questions =
            [
                new SurveyQuestion { Id = "q1", Text = "Genel olarak şirketimizde çalışmaktan ne kadar memnunsunuz?", Type = "rating" },
                new SurveyQuestion { Id = "q2", Text = "Yöneticinizden yeterli geri bildirim alıyor musunuz?", Type = "single", Options = ["Evet", "Kısmen", "Hayır"] },
                new SurveyQuestion { Id = "q3", Text = "Hangi yan haklar sizin için daha önemli?", Type = "multi", Options = ["Esnek çalışma", "Eğitim bütçesi", "Özel sağlık sigortası", "Yemek kartı"] },
                new SurveyQuestion { Id = "q4", Text = "Eklemek istedikleriniz", Type = "text", Required = false },
            ],
        };
        foreach (var u in p.Staff.Take(18))
        {
            var answers = new Dictionary<string, object> { ["q1"] = _rnd.Next(3, 6), ["q2"] = new[] { "Evet", "Kısmen", "Hayır" }[_rnd.Next(3)], ["q3"] = new[] { "Esnek çalışma", "Eğitim bütçesi" } };
            survey.Responses.Add(new SurveyResponse { RespondentHash = Hash($"{u.Id}:survey1"), Answers = JsonSerializer.Serialize(answers, Json), CreatedAt = _now.AddDays(-_rnd.Next(0, 5)) });
        }
        db.Surveys.Add(survey);
        db.Surveys.Add(new Survey
        {
            Title = "Yemekhane menü tercihi", Description = "Aralık ayı menüsünü birlikte belirleyelim.", IsAnonymous = false, EndsAt = _now.AddDays(5), CreatedBy = p.HrSpecialist,
            Questions = [new SurveyQuestion { Id = "q1", Text = "Hangi günlerin vejetaryen olmasını istersiniz?", Type = "multi", Options = ["Pazartesi", "Çarşamba", "Cuma"] }],
        });
        await db.SaveChangesAsync(ct);
    }

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    // =============================================================== OPERASYON
    private async Task SeedOperations(Dictionary<string, Department> depts, People p, string uploadRoot, CancellationToken ct)
    {
        string[] cities = ["Adana", "Adıyaman", "Afyon", "Aksaray", "Amasya", "Ankara", "Antalya", "Antakya", "Aydın", "Balıkesir", "Bartın", "Batman", "Bingöl", "Bolu", "Burdur", "Bursa",
            "Denizli", "Diyarbakır", "Düzce", "Edirne", "Elazığ", "Erzincan", "Erzurum", "Eskişehir", "Gaziantep", "Gebze", "Giresun", "Hatay", "Iğdır", "Isparta", "İskenderun", "İstanbul",
            "İzmir", "İzmit", "Kahramanmaraş", "K.Maraş", "Karabük", "Karaman", "Kars", "Kastamonu", "Kayseri", "Kilis", "Kırıkkale", "Kırklareli", "Konya", "Kütahya", "Malatya", "Manisa",
            "Mardin", "Mersin", "Muğla", "Muş", "Nevşehir", "Niğde", "Ordu", "Osmaniye", "Rize", "Sakarya", "Adapazarı", "Samsun", "Sinop", "Sivas", "Şanlıurfa", "Urfa", "Tekirdağ", "Tokat",
            "Trabzon", "Uşak", "Van", "Yozgat", "Zonguldak", "Alanya", "Pendik", "Maltepe", "Kağıthane", "Ümraniye", "Beylikdüzü", "Arnavutköy",
            "Kahramanmaraş", "Hatay", "Kocaeli", "Şanlıurfa", "İstanbul"];
        var istanbulDistricts = new HashSet<string> { "Gebze", "İzmit", "Pendik", "Maltepe", "Kağıthane", "Ümraniye", "Beylikdüzü", "Arnavutköy" };
        var storeNo = 0;
        foreach (var d in depts.Values.Where(d => d.Type == DepartmentType.Store).OrderBy(d => d.Name))
        {
            var city = cities.FirstOrDefault(c => d.Name.StartsWith(c, StringComparison.CurrentCultureIgnoreCase) || d.Name.Contains(" " + c + " ", StringComparison.CurrentCultureIgnoreCase));
            city = city switch
            {
                "K.Maraş" => "Kahramanmaraş", "Urfa" => "Şanlıurfa", "Antakya" or "İskenderun" => "Hatay", "Adapazarı" => "Sakarya", "Afyon" => "Afyonkarahisar",
                "Gebze" or "İzmit" => "Kocaeli", { } c when istanbulDistricts.Contains(c) => "İstanbul", _ => city,
            };
            db.Stores.Add(new Store
            {
                Code = $"M{++storeNo:000}", Name = d.Name, City = city, Department = d, Manager = d.Manager, Status = _rnd.NextDouble() < 0.04 ? StoreStatus.Renovation : StoreStatus.Open,
                AreaSqm = _rnd.Next(180, 1200), Phone = $"0 (212) {_rnd.Next(100, 999)} {_rnd.Next(10, 99)} {_rnd.Next(10, 99)}",
            });
        }

        var proj = new Project { Code = "PRJ-001", Name = "StajDesk Portal Geçişi", Description = "Tüm talep ve onay süreçlerinin portala taşınması.", Department = depts[FormCatalog.Depts.It], Owner = p.ItDirector, Status = ProjectStatus.Active, StartDate = DateOnly.FromDateTime(_now.AddDays(-60)), DueDate = DateOnly.FromDateTime(_now.AddDays(30)) };
        var proj2 = new Project { Code = "PRJ-002", Name = "Mağaza Ağ Altyapısı Yenileme", Description = "40 mağazada switch ve AP değişimi.", Department = depts[FormCatalog.Depts.It], Owner = p.ItAgents[2], Status = ProjectStatus.Active, StartDate = DateOnly.FromDateTime(_now.AddDays(-20)), DueDate = DateOnly.FromDateTime(_now.AddDays(90)) };
        var proj3 = new Project { Code = "PRJ-003", Name = "Yılsonu Sayım Hazırlığı", Department = depts[FormCatalog.Depts.Logistics], Owner = p.Staff.First(u => u.Department!.Name == FormCatalog.Depts.Logistics), Status = ProjectStatus.Planned, StartDate = DateOnly.FromDateTime(_now.AddDays(10)), DueDate = DateOnly.FromDateTime(_now.AddDays(45)) };
        db.Projects.AddRange(proj, proj2, proj3);
        WorkTask T(Project? pr, string title, User? assignee, WorkTaskStatus s, int due, TicketPriority pri = TicketPriority.Medium) =>
            new() { Project = pr, Title = title, Assignee = assignee, CreatedBy = pr?.Owner ?? p.Manager, Status = s, Priority = pri, DueDate = DateOnly.FromDateTime(_now.AddDays(due)), CompletedAt = s == WorkTaskStatus.Done ? _now.AddDays(-_rnd.Next(1, 10)) : null };
        db.Tasks.AddRange(
            T(proj, "Departman formlarının tanımlanması", p.ItAgents[3], WorkTaskStatus.Done, -5),
            T(proj, "Kullanıcı eğitim videolarının hazırlanması", p.HrSpecialist, WorkTaskStatus.InProgress, 7),
            T(proj, "Eski form arşivinin taşınması", p.ItAgents[0], WorkTaskStatus.Todo, 14),
            T(proj, "Yetki matrisinin onaylanması", p.ItDirector, WorkTaskStatus.Review, 3, TicketPriority.High),
            T(proj2, "Mağaza envanter keşfi", p.ItAgents[2], WorkTaskStatus.InProgress, 10),
            T(proj2, "Tedarikçi teklif karşılaştırması", p.ItAgents[1], WorkTaskStatus.Todo, 21),
            T(proj3, "Sayım ekiplerinin belirlenmesi", null, WorkTaskStatus.Todo, 15),
            T(null, "Aylık kapanış mutabakatı", p.Employee, WorkTaskStatus.InProgress, 2, TicketPriority.High),
            T(null, "Banka ekstrelerinin kontrolü", p.Employee, WorkTaskStatus.Todo, 4),
            T(null, "Tedarikçi cari hesap kontrolü", p.Employee, WorkTaskStatus.Done, -2));

        // Nöbetçi personel: bu hafta ve gelecek hafta
        var weekStart = _now.Date.AddDays(-(int)_now.DayOfWeek + 1);
        for (var w = 0; w < 3; w++)
        {
            db.OnCallShifts.Add(new OnCallShift { User = p.ItAgents[w % 3], Team = "BT Destek", StartsAt = weekStart.AddDays(7 * w), EndsAt = weekStart.AddDays(7 * w + 7), Note = "Mesai dışı acil durumlar için" });
            db.OnCallShifts.Add(new OnCallShift { User = p.ItAgents[2 - w % 3 == 2 ? 2 : 1], Team = "Ağ ve Altyapı", StartsAt = weekStart.AddDays(7 * w), EndsAt = weekStart.AddDays(7 * w + 7) });
            db.OnCallShifts.Add(new OnCallShift { User = p.HrSpecialist, Team = "İdari İşler", StartsAt = weekStart.AddDays(7 * w), EndsAt = weekStart.AddDays(7 * w + 7) });
        }

        // Mesajlar
        db.Messages.AddRange(
            new DirectMessage { Sender = p.Manager, Recipient = p.Employee, Body = "Ahmet merhaba, ay sonu mutabakatı için toplantıyı yarın 10:00'a alabilir miyiz?", CreatedAt = _now.AddHours(-5) },
            new DirectMessage { Sender = p.Employee, Recipient = p.Manager, Body = "Tabii Hakan Bey, takvime ekliyorum.", CreatedAt = _now.AddHours(-4), ReadAt = _now.AddHours(-3) },
            new DirectMessage { Sender = p.ItAgents[0], Recipient = p.Employee, Body = "Monitör talebiniz için stoğu kontrol ettim, perşembe teslim edebilirim.", CreatedAt = _now.AddHours(-1) });

        // Dokümanlar (gerçek dosya olarak diske yazılır)
        Directory.CreateDirectory(uploadRoot);
        StoredFile Doc(string name, string content)
        {
            var id = Guid.NewGuid();
            var rel = Path.Combine("seed", $"{id}.txt");
            Directory.CreateDirectory(Path.Combine(uploadRoot, "seed"));
            File.WriteAllText(Path.Combine(uploadRoot, rel), content, Encoding.UTF8);
            return new StoredFile { Id = id, FileName = name, ContentType = "text/plain", Size = Encoding.UTF8.GetByteCount(content), StoragePath = rel, UploadedBy = p.HrSpecialist };
        }
        db.Documents.AddRange(
            new Document { Title = "Çalışan El Kitabı", Description = "Çalışma saatleri, izinler, yan haklar ve davranış kuralları.", Category = "İK Politikaları", File = Doc("calisan-el-kitabi.txt", "DEVOPLUS ÇALIŞAN EL KİTABI\n\n1. Çalışma saatleri: 08:30 - 17:30\n2. Yıllık izin: 4857 sayılı İş Kanunu'na göre kıdeme bağlı\n3. Uzaktan çalışma: Haftada 2 gün, yönetici onayı ile\n"), UploadedBy = p.HrSpecialist },
            new Document { Title = "Bilgi Güvenliği Politikası", Description = "Şifre, cihaz ve veri kullanım kuralları.", Category = "BT Politikaları", File = Doc("bilgi-guvenligi-politikasi.txt", "BİLGİ GÜVENLİĞİ POLİTİKASI\n\n- Şifreler en az 10 karakter olmalıdır.\n- Şirket verileri kişisel bulut hesaplarına aktarılamaz.\n- Kayıp/çalıntı cihazlar derhal IT'ye bildirilmelidir.\n"), UploadedBy = p.ItDirector },
            new Document { Title = "Masraf Yönetmeliği", Description = "Masraf ve avans süreçleri, limitler.", Category = "Finans", File = Doc("masraf-yonetmeligi.txt", "MASRAF YÖNETMELİĞİ\n\nMasraf belgeleri 30 gün içinde StajDesk üzerinden bildirilmelidir.\n"), UploadedBy = p.Manager },
            new Document { Title = "Muhasebe Kapanış Takvimi", Description = "Yalnızca muhasebe departmanı.", Category = "Finans", File = Doc("kapanis-takvimi.txt", "Aylık kapanış: her ayın 3. iş günü.\n"), Department = depts[FormCatalog.Depts.Accounting], UploadedBy = p.Manager });

        // Hesap talebi (İK → IT)
        db.AccountRequests.Add(new AccountRequest
        {
            RequestedBy = p.HrSpecialist, FirstName = "Selin", LastName = "Aksoy", Email = "selin.aksoy@devoplus.local", Department = depts[FormCatalog.Depts.Logistics],
            StartDate = DateOnly.FromDateTime(_now.AddDays(5)), EmploymentType = EmploymentType.FullTime, Note = "Lojistik operasyon ekibine katılacak.", Status = AccountRequestStatus.Pending,
            Manager = depts[FormCatalog.Depts.Logistics].Manager, CreatedAt = _now.AddHours(-20),
        });

        // Örnek bildirimler
        foreach (var t in await db.Tickets.Where(x => x.Status == TicketStatus.Resolved).OrderBy(x => x.Id).Take(6).ToListAsync(ct))
            db.Notifications.Add(new Notification { UserId = t.RequesterId, Title = "Talebiniz çözüldü", Message = $"#{t.Id} \"{t.Title}\" talebiniz çözüldü olarak işaretlendi. Lütfen onaylayınız.", Kind = "success", Link = $"/tickets/{t.Id}", CreatedAt = t.ResolvedAt ?? _now });
        db.Notifications.Add(new Notification { UserId = p.Employee.Id, Title = "Yeni mesaj", Message = "Elif Aydın size bir mesaj gönderdi.", Kind = "message", Link = "/messages", CreatedAt = _now.AddHours(-1) });
        db.Notifications.Add(new Notification { UserId = p.Manager.Id, Title = "Onay bekleyen başvuru", Message = "Ahmet Yılmaz izin talebi oluşturdu.", Kind = "approval", Link = "/approvals", CreatedAt = _now.AddDays(-1) });
        await db.SaveChangesAsync(ct);
    }
}
