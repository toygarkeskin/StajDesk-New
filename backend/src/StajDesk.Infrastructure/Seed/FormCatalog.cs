using StajDesk.Domain;

namespace StajDesk.Infrastructure.Seed;

/// <summary>
/// Departman uygulamalarının başlangıç tanımları. Hepsi aynı form + iş akışı motoruyla çalışır;
/// yöneticiler bu tanımları arayüzdeki form oluşturucudan değiştirebilir.
/// </summary>
public static class FormCatalog
{
    public static class Depts
    {
        public const string It = "Bilgi Teknolojileri ve Dijital Dönüşüm Direktörlüğü";
        public const string Hr = "İnsan Kaynakları ve İdari İşler Direktörlüğü";
        public const string Legal = "Hukuk Müşavirliği";
        public const string Quality = "Kalite Yönetimi";
        public const string Strategy = "Strateji ve Dönüşüm Ofisi";
        public const string Logistics = "Lojistik";
        public const string Accounting = "Muhasebe Departmanı";
        public const string Marketing = "Pazarlama ve CRM Departmanı";
        public const string Purchasing = "Satın Alma Departmanı";
        public const string Product = "Kategori Departmanı";
        public const string Management = "Üst Yönetim";
        public const string Export = "Yurtdışı Satış ve Operasyon Departmanı";
        public const string SalesSupport = "Satış Destek Departmanı";
    }

    public record Def(string Key, string Name, string Module, string Owner, string Icon, string Color, string Description,
        List<FormField> Fields, List<WorkflowStep> Steps, string Kind = FormKinds.Standard);

    // ---- alan yardımcıları ----
    private static FormField Text(string key, string label, bool req = false, string? ph = null) => new() { Key = key, Label = label, Type = FieldTypes.Text, Required = req, Placeholder = ph, MaxLength = 200 };
    private static FormField Area(string key, string label, bool req = false, string? help = null) => new() { Key = key, Label = label, Type = FieldTypes.Textarea, Required = req, HelpText = help, MaxLength = 4000 };
    private static FormField Num(string key, string label, bool req = false, decimal? min = null, decimal? max = null) => new() { Key = key, Label = label, Type = FieldTypes.Number, Required = req, Min = min, Max = max };
    private static FormField Money(string key, string label, bool req = false, decimal? max = null) => new() { Key = key, Label = label, Type = FieldTypes.Currency, Required = req, Min = 0, Max = max };
    private static FormField Date(string key, string label, bool req = false) => new() { Key = key, Label = label, Type = FieldTypes.Date, Required = req };
    private static FormField Pick(string key, string label, bool req, params string[] opts) => new() { Key = key, Label = label, Type = FieldTypes.Select, Required = req, Options = [.. opts] };
    private static FormField Multi(string key, string label, bool req, params string[] opts) => new() { Key = key, Label = label, Type = FieldTypes.MultiSelect, Required = req, Options = [.. opts] };
    private static FormField Check(string key, string label) => new() { Key = key, Label = label, Type = FieldTypes.Checkbox };
    private static FormField File(string key, string label, bool req = false) => new() { Key = key, Label = label, Type = FieldTypes.File, Required = req, HelpText = "PDF, görsel, Word/Excel; en fazla 10 MB" };
    private static FormField Person(string key, string label, bool req = false, string? help = null) => new() { Key = key, Label = label, Type = FieldTypes.User, Required = req, HelpText = help };
    private static FormField Info(string key, string text) => new() { Key = key, Label = text, Type = FieldTypes.Description };

    // ---- onay adımı yardımcıları ----
    private static WorkflowStep Manager => new() { Name = "Yönetici Onayı", ApproverType = ApproverType.Manager };
    private static WorkflowStep Owner(string name) => new() { Name = name, ApproverType = ApproverType.ResponsibleDepartment };
    private static WorkflowStep FieldPerson(string name, string fieldKey) => new() { Name = name, ApproverType = ApproverType.FieldUser, FieldKey = fieldKey };
    private static WorkflowStep Role(string name, string code) => new() { Name = name, ApproverType = ApproverType.Role, RoleCode = code };

    private static readonly string[] Urgency = ["Düşük", "Normal", "Yüksek", "Acil"];

    public static IReadOnlyList<Def> All { get; } =
    [
        // ===================== IT =====================
        new("hardware-request", "Donanım Talebi", "IT", Depts.It, "Monitor", "#2563eb", "Bilgisayar, monitör, telefon ve çevre birimi talepleri.",
            [Pick("deviceType", "Cihaz türü", true, "Dizüstü Bilgisayar", "Masaüstü Bilgisayar", "Monitör", "Klavye / Mouse", "Yazıcı", "Cep Telefonu", "El Terminali", "Diğer"),
             Num("quantity", "Adet", true, 1, 20), Date("neededBy", "İhtiyaç tarihi"), Area("justification", "Gerekçe", true)],
            [Manager, Owner("BT Onayı ve Temin")]),
        new("access-request", "Erişim Yönetimi", "IT", Depts.It, "KeyRound", "#2563eb", "Sistem, klasör ve uygulama yetki talepleri.",
            [Pick("system", "Sistem", true, "ERP (SAP)", "CRM", "WMS", "Ortak Klasör", "VPN", "E-posta Dağıtım Listesi", "Power BI", "Diğer"),
             Pick("accessLevel", "Yetki seviyesi", true, "Okuma", "Okuma / Yazma", "Yönetici"),
             Text("resource", "Kaynak / klasör yolu", false, "ör. \\\\fs01\\Finans"), Date("endDate", "Yetki bitiş tarihi (geçiciyse)"), Area("reason", "Gerekçe", true)],
            [Manager, Owner("BT Güvenlik Onayı")]),
        new("wms-device", "WMS Cihaz", "IT", Depts.It, "ScanBarcode", "#2563eb", "Depo yönetim sistemi el terminali ve yazıcı talepleri.",
            [Text("warehouse", "Depo / lokasyon", true), Pick("deviceType", "Cihaz", true, "El Terminali", "Barkod Yazıcı", "Etiket Yazıcı", "Mobil Yazıcı"),
             Pick("requestType", "Talep türü", true, "Yeni Cihaz", "Arıza / Değişim", "İade"), Text("serialNumber", "Mevcut cihaz seri no"), Area("description", "Açıklama", true)],
            [Manager, Owner("BT Onayı")]),

        // ===================== Genel Süreçler =====================
        new("legal-process", "Hukuki Süreç", "Genel Süreçler", Depts.Legal, "Scale", "#7c3aed", "Sözleşme inceleme, ihtarname ve dava süreçleri.",
            [Text("subject", "Konu", true), Pick("processType", "Süreç türü", true, "Sözleşme İnceleme", "İhtarname", "Dava Takibi", "KVKK Başvurusu", "Diğer"),
             Text("counterparty", "Karşı taraf"), Pick("urgency", "Aciliyet", true, Urgency), Area("description", "Açıklama", true), File("document", "İlgili belge")],
            [Manager, Owner("Hukuk Değerlendirmesi")]),
        new("suggestion", "Öneri", "Genel Süreçler", Depts.Strategy, "Lightbulb", "#7c3aed", "İyileştirme ve yenilik önerileri.",
            [Text("title", "Öneri başlığı", true), Pick("area", "Alan", true, "Verimlilik", "Maliyet", "Müşteri Deneyimi", "Çalışan Deneyimi", "İş Güvenliği", "Diğer"),
             Area("currentSituation", "Mevcut durum"), Area("proposal", "Önerim", true), Area("expectedBenefit", "Beklenen fayda")],
            [Owner("Öneri Komitesi Değerlendirmesi")]),
        new("near-miss", "Ramak Kala", "Genel Süreçler", Depts.Hr, "TriangleAlert", "#7c3aed", "İş güvenliği ramak kala olay bildirimi.",
            [Info("info_nearmiss", "Ramak kala: yaralanma veya hasarla sonuçlanmamış ancak sonuçlanabilecek olaylardır. Acil durumlarda önce 112'yi arayınız."),
             Text("location", "Olay yeri", true), Date("eventDate", "Olay tarihi", true),
             Pick("category", "Kategori", true, "Kayma / Düşme", "Elektrik", "Yangın", "Ekipman", "Depolama / İstif", "Diğer"),
             Pick("severity", "Potansiyel ciddiyet", true, "Düşük", "Orta", "Yüksek"), Area("description", "Ne oldu?", true), Area("suggestedAction", "Önerilen önlem"), File("photo", "Fotoğraf")],
            [Owner("İSG Değerlendirmesi")]),
        new("process-improvement", "Süreç İyileştirme", "Genel Süreçler", Depts.Strategy, "Workflow", "#7c3aed", "Mevcut iş süreçlerinin iyileştirilmesi.",
            [Text("processName", "Süreç adı", true), Area("problem", "Sorun", true), Area("proposedSolution", "Önerilen çözüm", true),
             Money("expectedSaving", "Tahmini yıllık tasarruf (₺)"), Pick("priority", "Öncelik", true, Urgency)],
            [Manager, Owner("Strateji Ofisi Değerlendirmesi")]),

        // ===================== İK ve İdari İşler =====================
        new("leave", "İzin", "İnsan Kaynakları ve İdari İşler", Depts.Hr, "CalendarDays", "#059669", "Yıllık, mazeret ve diğer izin talepleri. İş günü ve bakiye otomatik hesaplanır.",
            [Pick("leaveType", "İzin türü", true, Enum.GetValues<LeaveType>().Select(LeaveRules.Label).ToArray()),
             Date("startDate", "Başlangıç", true), Date("endDate", "Bitiş", true), Text("backupPerson", "Yerime bakacak kişi"), Area("description", "Açıklama")],
            [Manager, Role("İK Onayı", RoleCodes.Hr)], FormKinds.Leave),
        new("onboarding", "İşe Giriş", "İnsan Kaynakları ve İdari İşler", Depts.Hr, "UserPlus", "#059669", "Yeni personelin ekipman ve oryantasyon hazırlıkları.",
            [Info("info_onboarding", "Sistem hesabı için ayrıca Kullanıcılar → Hesap Talepleri ekranından talep açılmalıdır."),
             Text("employeeName", "Personel adı soyadı", true), Date("startDate", "İşe başlama", true), Text("departmentName", "Departman / mağaza", true), Text("title", "Unvan", true),
             Multi("equipment", "Hazırlanacaklar", false, "Dizüstü Bilgisayar", "Cep Telefonu", "Yaka Kartı", "Üniforma", "Masa / Dolap", "Araç"), Area("notes", "Notlar")],
            [Owner("İK Hazırlık")]),
        new("offboarding", "İşten Çıkış", "İnsan Kaynakları ve İdari İşler", Depts.Hr, "UserMinus", "#059669", "Ayrılış süreci, zimmet iadeleri ve devir teslim.",
            [Text("employeeName", "Personel adı soyadı", true), Date("lastWorkDay", "Son çalışma günü", true),
             Pick("reason", "Ayrılış nedeni", true, "İstifa", "Emeklilik", "Sözleşme Sonu", "Fesih", "Diğer"), Area("assetsToReturn", "İade edilecek zimmetler"), Area("handoverNotes", "Devir teslim notları")],
            [Manager, Owner("İK Onayı")]),
        new("personnel-card", "Personel Kartı", "İnsan Kaynakları ve İdari İşler", Depts.Hr, "IdCard", "#059669", "Özlük bilgisi güncelleme talepleri.",
            [Pick("updateType", "Güncellenecek bilgi", true, "Adres", "Telefon", "Banka Hesabı (IBAN)", "Medeni Durum", "Acil Durum Kişisi", "Eğitim Bilgisi"),
             Area("newValue", "Yeni bilgi", true), Date("effectiveDate", "Geçerlilik tarihi"), File("document", "Destekleyici belge")],
            [Owner("İK Özlük")]),
        new("training", "Eğitim", "İnsan Kaynakları ve İdari İşler", Depts.Hr, "GraduationCap", "#059669", "Eğitim, sertifika ve konferans katılım talepleri.",
            [Text("trainingName", "Eğitim adı", true), Text("provider", "Eğitim kurumu"), Pick("trainingType", "Tür", true, "Online", "Sınıf İçi", "Konferans", "Sertifika Programı"),
             Date("startDate", "Başlangıç"), Money("cost", "Ücret (₺)"), Area("justification", "Gerekçe", true)],
            [Manager, Owner("İK Eğitim ve Gelişim")]),
        new("vehicle", "Araç", "İnsan Kaynakları ve İdari İşler", Depts.Hr, "Car", "#059669", "Şirket aracı ve şoför talepleri.",
            [Text("purpose", "Kullanım amacı", true), Date("startDate", "Başlangıç", true), Date("endDate", "Bitiş", true), Text("destination", "Gidilecek yer", true),
             Num("passengerCount", "Yolcu sayısı", false, 1, 15), Check("driverNeeded", "Şoför talep ediyorum")],
            [Manager, Owner("İdari İşler")]),
        new("travel", "Seyahat", "İnsan Kaynakları ve İdari İşler", Depts.Hr, "Plane", "#059669", "İş seyahati ulaşım ve konaklama talepleri.",
            [Text("destination", "Gidilecek şehir / ülke", true), Date("departureDate", "Gidiş", true), Date("returnDate", "Dönüş", true),
             Pick("transport", "Ulaşım", true, "Uçak", "Otobüs", "Tren", "Şirket Aracı"), Check("accommodation", "Konaklama gerekli"), Money("estimatedCost", "Tahmini maliyet (₺)"), Area("purpose", "Seyahat amacı", true)],
            [Manager, Owner("İdari İşler")]),
        new("shift", "Vardiya", "İnsan Kaynakları ve İdari İşler", Depts.Hr, "Clock", "#059669", "Vardiya değişikliği ve takas talepleri. Takas yapılan çalışanın onayı ve izin çakışması otomatik kontrol edilir.",
            [Text("unit", "Mağaza / birim", true), Date("date", "Tarih", true),
             Pick("currentShift", "Mevcut vardiya", true, "Sabah (09:00-17:00)", "Ara (11:00-19:00)", "Akşam (14:00-22:00)"),
             Pick("requestedShift", "İstenen vardiya", true, "Sabah (09:00-17:00)", "Ara (11:00-19:00)", "Akşam (14:00-22:00)", "İzin"),
             Person("swapWith", "Takas yapılacak kişi", false, "Seçerseniz talep önce bu kişinin onayına gider. Aynı departmandan olmalıdır."), Area("reason", "Gerekçe")],
            [FieldPerson("Takas Onayı", "swapWith"), Manager], FormKinds.Shift),
        new("uniform", "Kıyafet", "İnsan Kaynakları ve İdari İşler", Depts.Hr, "Shirt", "#059669", "Üniforma ve iş kıyafeti talepleri.",
            [Pick("item", "Ürün", true, "Mağaza Üniforması", "Polar", "Mont", "İş Ayakkabısı", "Yaka Kartı İpi"), Pick("size", "Beden", true, "XS", "S", "M", "L", "XL", "XXL"),
             Num("quantity", "Adet", true, 1, 5), Pick("reason", "Neden", true, "Yeni Personel", "Yıpranma", "Beden Değişimi")],
            [Manager, Owner("İdari İşler")]),

        // ===================== Kalite =====================
        new("sspg-entry", "SSPG Veri Girişi", "Kalite", Depts.Quality, "ClipboardCheck", "#d97706", "Satış sonrası performans göstergesi veri girişi.",
            [Text("storeName", "Mağaza", true), Date("period", "Dönem", true), Pick("metric", "Gösterge", true, "Müşteri Memnuniyeti", "Ürün İade Oranı", "Kalite Denetim Puanı", "Şikâyet Sayısı"),
             Num("value", "Değer", true, 0, 100000), Area("notes", "Notlar")],
            [Owner("Kalite Kontrol")]),
        new("bulk-data-entry", "Toplu Veri Girişi", "Kalite", Depts.Quality, "FileSpreadsheet", "#d97706", "Kalite verilerinin dosya ile toplu yüklenmesi.",
            [Pick("dataType", "Veri türü", true, "Kalite Denetimi", "Ürün İnceleme", "Mağaza Denetimi"), Date("period", "Dönem", true), File("file", "Veri dosyası (CSV/XLSX)", true), Area("description", "Açıklama")],
            [Owner("Kalite Kontrol")]),

        // ===================== Lojistik =====================
        new("logistics-supplier-prices", "Tedarikçi Fiyatları", "Lojistik", Depts.Logistics, "Truck", "#0891b2", "Nakliye, depolama ve kargo tedarikçi fiyat bildirimi.",
            [Text("supplier", "Tedarikçi", true), Pick("serviceType", "Hizmet", true, "Nakliye", "Depolama", "Kargo", "Gümrük"), Text("route", "Güzergâh / kapsam"),
             Money("unitPrice", "Birim fiyat", true), Pick("currency", "Para birimi", true, "TRY", "USD", "EUR"), Date("validUntil", "Geçerlilik"), File("offer", "Teklif dosyası")],
            [Manager, Owner("Lojistik Onayı")]),
        new("auxiliary-materials", "Yardımcı Malzemeler", "Lojistik", Depts.Logistics, "Package", "#0891b2", "Koli, streç, askı, etiket gibi sarf malzeme talepleri.",
            [Pick("material", "Malzeme", true, "Koli", "Streç Film", "Askı", "Etiket", "Poşet", "Koli Bandı"), Num("quantity", "Miktar", true, 1, 100000),
             Pick("unit", "Birim", true, "Adet", "Koli", "Rulo"), Text("deliveryLocation", "Teslim yeri", true), Date("neededBy", "İhtiyaç tarihi")],
            [Owner("Lojistik Sevkiyat")]),

        // ===================== Mağaza Muhasebesi =====================
        new("store-revenue", "Ciro", "Mağaza Muhasebesi", Depts.Accounting, "Receipt", "#dc2626", "Günlük mağaza ciro bildirimi ve mutabakatı. Nakit + kart toplamı, mükerrer bildirim ve ortalama sepet otomatik kontrol edilir.",
            [Text("storeName", "Mağaza", true), Date("date", "Tarih", true), Money("cashAmount", "Nakit (₺)"), Money("cardAmount", "Kredi kartı (₺)"),
             Money("amount", "Toplam ciro (₺)", true), Num("receiptCount", "Fiş adedi", false, 0, 100000), Area("notes", "Notlar")],
            [Owner("Mağaza Muhasebesi Kontrolü")], FormKinds.Revenue),

        // ===================== Muhasebe =====================
        new("advance", "Avans", "Muhasebe", Depts.Accounting, "Banknote", "#dc2626", "İş avansı talepleri. Tanımlı limit otomatik kontrol edilir.",
            [Money("amount", "Tutar (₺)", true), Pick("repaymentPlan", "Geri ödeme", true, "Tek Seferde", "2 Taksit", "3 Taksit"), Date("neededBy", "İhtiyaç tarihi"), Area("reason", "Gerekçe", true)],
            [Manager, Owner("Muhasebe Onayı")], FormKinds.Advance),
        new("expense", "Masraf", "Muhasebe", Depts.Accounting, "Wallet", "#dc2626", "Masraf bildirimi. Belge zorunludur, limit otomatik kontrol edilir.",
            [Date("expenseDate", "Masraf tarihi", true), Pick("category", "Kategori", true, "Yol", "Konaklama", "Yemek", "Temsil / Ağırlama", "Kırtasiye", "Diğer"),
             Money("amount", "Tutar (₺)", true), Area("description", "Açıklama", true), File("receipt", "Fiş / fatura", true)],
            [Manager, Owner("Muhasebe Onayı")], FormKinds.Expense),
        new("insurance", "Sigorta", "Muhasebe", Depts.Accounting, "ShieldCheck", "#dc2626", "Sağlık sigortası, kasko ve hasar bildirimi.",
            [Pick("insuranceType", "İşlem", true, "Özel Sağlık Sigortası - Aile Ekleme", "Araç Kaskosu", "Ferdi Kaza", "Hasar Bildirimi"), Text("insuredPerson", "Sigortalı"),
             Date("startDate", "Başlangıç"), Area("description", "Açıklama", true), File("document", "Belge")],
            [Owner("Muhasebe Sigorta")]),

        // ===================== Pazarlama =====================
        new("crm-enrichment", "CRM Veri Zenginleştirme", "Pazarlama", Depts.Marketing, "Database", "#db2777", "Müşteri verisi zenginleştirme ve segment talepleri.",
            [Pick("segment", "Segment", true, "Sadakat Üyeleri", "Yeni Müşteriler", "Pasif Müşteriler", "Kurumsal"),
             Multi("requestedFields", "Zenginleştirilecek alanlar", true, "E-posta", "Telefon", "Doğum Tarihi", "Şehir", "İleti İzni"),
             Num("recordCount", "Tahmini kayıt sayısı", false, 1, 10000000), Area("purpose", "Amaç (KVKK uyumu için zorunlu)", true), File("dataFile", "Kaynak dosya")],
            [Manager, Owner("CRM Onayı")]),
        new("communication", "İletişim", "Pazarlama", Depts.Marketing, "Megaphone", "#db2777", "SMS, e-posta, push ve mağaza içi iletişim talepleri.",
            [Text("campaignName", "Kampanya / iletişim adı", true), Multi("channel", "Kanal", true, "SMS", "E-posta", "Push Bildirim", "Sosyal Medya", "Mağaza İçi Görsel"),
             Text("audience", "Hedef kitle"), Date("sendDate", "Gönderim tarihi", true), Area("content", "İçerik", true), File("visual", "Görsel")],
            [Manager, Owner("Pazarlama Onayı")]),

        // ===================== Satın Alma =====================
        new("work-order", "İş Emirleri", "Satın Alma", Depts.Purchasing, "Wrench", "#4f46e5", "Bakım, onarım, tadilat ve teknik servis iş emirleri.",
            [Pick("workType", "İş türü", true, "Bakım-Onarım", "Tadilat", "Temizlik", "Teknik Servis", "Nakliye"), Text("location", "Lokasyon", true),
             Pick("urgency", "Aciliyet", true, Urgency), Money("amount", "Tahmini maliyet (₺)"), Area("description", "Açıklama", true)],
            [Manager, Owner("Satın Alma Onayı")]),
        new("purchase-quote", "Teklifler", "Satın Alma", Depts.Purchasing, "FileSearch", "#4f46e5", "Ürün ve hizmet için teklif toplama talebi.",
            [Text("item", "Ürün / hizmet", true), Num("quantity", "Miktar", true, 1, 1000000), Area("specs", "Teknik özellikler"),
             Text("suggestedSuppliers", "Önerilen tedarikçiler"), Money("amount", "Bütçe (₺)"), Date("neededBy", "İhtiyaç tarihi")],
            [Manager, Owner("Satın Alma Değerlendirmesi")]),
        new("purchase-supplier-prices", "Tedarikçi Fiyatları", "Satın Alma", Depts.Purchasing, "Tags", "#4f46e5", "Satın alma tedarikçi fiyat listesi bildirimi.",
            [Text("supplier", "Tedarikçi", true), Text("product", "Ürün", true), Money("unitPrice", "Birim fiyat", true), Pick("currency", "Para birimi", true, "TRY", "USD", "EUR"),
             Date("validUntil", "Geçerlilik"), File("priceList", "Fiyat listesi")],
            [Owner("Satın Alma Onayı")]),

        // ===================== Ürün =====================
        new("store-rename", "Mağaza İsim Değişikliği", "Ürün", Depts.Product, "Store", "#ea580c", "Mağaza isim ve tabela değişikliği talepleri.",
            [Text("storeCode", "Mağaza kodu", true), Text("currentName", "Mevcut isim", true), Text("newName", "Yeni isim", true), Date("effectiveDate", "Geçerlilik tarihi", true), Area("reason", "Gerekçe", true)],
            [Manager, Owner("Ürün Yönetimi Onayı")]),

        // ===================== Yönetim =====================
        new("invoice", "Fatura", "Yönetim", Depts.Management, "FileText", "#475569", "Yönetim onayı gerektiren fatura onayları.",
            [Text("supplier", "Tedarikçi", true), Text("invoiceNo", "Fatura no", true), Date("invoiceDate", "Fatura tarihi", true), Money("amount", "Tutar (₺)", true),
             Text("costCenter", "Masraf merkezi"), File("invoiceFile", "Fatura", true)],
            [Manager, Owner("Yönetim Onayı")]),
        new("survey-request", "Anket", "Yönetim", Depts.Management, "ClipboardList", "#475569", "Çalışan anketi yayınlama talebi.",
            [Text("topic", "Anket konusu", true), Pick("audience", "Hedef kitle", true, "Tüm Çalışanlar", "Merkez Ofis", "Mağazalar", "Belirli Departman"),
             Area("questions", "Önerilen sorular", true), Date("deadline", "Son yanıt tarihi")],
            [Owner("Yönetim Onayı")]),
        new("management-suggestion", "Öneri (Yönetim)", "Yönetim", Depts.Management, "Lightbulb", "#475569", "Doğrudan üst yönetime iletilen öneriler.",
            [Text("title", "Başlık", true), Area("proposal", "Öneri", true), Area("expectedBenefit", "Beklenen fayda")],
            [Owner("Üst Yönetim Değerlendirmesi")]),
        new("management-sspg", "SSPG", "Yönetim", Depts.Management, "BarChart3", "#475569", "SSPG sonuçlarının yönetim değerlendirmesi.",
            [Date("period", "Dönem", true), Text("scope", "Kapsam (bölge / mağaza)", true), Num("score", "Puan", true, 0, 100), Area("comment", "Değerlendirme", true)],
            [Owner("Yönetim Değerlendirmesi")]),
        new("legal-texts", "Hukuki Metinler", "Yönetim", Depts.Legal, "ScrollText", "#475569", "KVKK, sözleşme ve kampanya metinlerinin onayı.",
            [Pick("textType", "Metin türü", true, "KVKK Aydınlatma Metni", "Gizlilik Sözleşmesi", "Kampanya Katılım Koşulları", "Çerez Politikası", "Diğer"),
             Area("purpose", "Kullanım amacı", true), Date("publishDate", "Yayın tarihi"), File("draft", "Taslak metin", true)],
            [Owner("Hukuk İncelemesi"), Role("Üst Yönetim Onayı", RoleCodes.GeneralManager)]),

        // ===================== Strateji ve Dönüşüm Ofisi =====================
        new("project-request", "Proje ve Geliştirme Talepleri", "Strateji ve Dönüşüm Ofisi", Depts.Strategy, "Rocket", "#0f766e", "Yeni yazılım, otomasyon ve dijitalleşme talepleri.",
            [Text("projectName", "Proje adı", true), Pick("requestType", "Talep türü", true, "Yeni Yazılım / Modül", "Mevcut Sistem Geliştirme", "Raporlama", "Otomasyon", "Süreç Dijitalleştirme"),
             Area("problem", "Çözülecek problem", true), Area("expectedBenefit", "Beklenen fayda"), Num("affectedUsers", "Etkilenen kullanıcı sayısı", false, 1, 100000), Date("desiredDate", "İstenen tarih")],
            [Manager, Owner("Strateji Ofisi Önceliklendirme")]),

        // ===================== Yurtdışı Satış =====================
        new("export-order", "Ürün Siparişleri", "Yurtdışı Satış", Depts.Export, "Globe", "#9333ea", "Yurtdışı bayi ve müşteri ürün siparişleri.",
            [Pick("country", "Ülke", true, "Irak", "Azerbaycan", "Gürcistan", "KKTC", "Almanya", "Diğer"), Text("customer", "Müşteri", true), Text("productCode", "Ürün / model kodu", true),
             Num("quantity", "Adet", true, 1, 1000000), Money("unitPrice", "Birim fiyat"), Pick("currency", "Para birimi", true, "USD", "EUR"), Date("deliveryDate", "Teslim tarihi")],
            [Manager, Owner("Yurtdışı Operasyon Onayı")]),

        // ===================== Yurtiçi Satış =====================
        new("gift-card", "Hediye Kartı", "Yurtiçi Satış", Depts.SalesSupport, "Gift", "#16a34a", "Kurumsal hediye kartı talepleri.",
            [Text("company", "Firma", true), Num("cardCount", "Kart adedi", true, 1, 10000), Money("cardValue", "Kart tutarı (₺)", true), Date("deliveryDate", "Teslim tarihi"), Area("invoiceInfo", "Fatura bilgileri", true)],
            [Manager, Owner("Satış Destek Onayı")]),
        new("store-invoice-approval", "Mağaza Fatura Onayı", "Yurtiçi Satış", Depts.SalesSupport, "FileCheck", "#16a34a", "Mağaza kaynaklı fatura onayları.",
            [Text("storeName", "Mağaza", true), Text("invoiceNo", "Fatura no", true), Money("amount", "Tutar (₺)", true), Area("description", "Açıklama"), File("invoiceFile", "Fatura", true)],
            [Manager, Owner("Satış Destek Onayı")]),
        new("badge", "Yaka Kartı", "Yurtiçi Satış", Depts.SalesSupport, "BadgeCheck", "#16a34a", "Mağaza personeli yaka kartı talepleri.",
            [Text("employeeName", "Personel", true), Text("unit", "Mağaza / departman", true), Text("title", "Unvan", true),
             Pick("reason", "Neden", true, "Yeni Personel", "Kayıp", "Hasar", "Unvan Değişikliği"), File("photo", "Vesikalık fotoğraf")],
            [Manager, Owner("Satış Destek")]),
        new("sales-transfer", "Satış Transferleri", "Yurtiçi Satış", Depts.SalesSupport, "ArrowLeftRight", "#16a34a", "Mağazalar arası satış/ciro transferleri.",
            [Text("fromStore", "Kaynak mağaza", true), Text("toStore", "Hedef mağaza", true), Date("transferDate", "Tarih", true), Money("amount", "Tutar (₺)", true), Area("reason", "Gerekçe", true)],
            [Manager, Owner("Satış Destek Onayı")]),

        // ===================== Diğer =====================
        new("receipt-scan", "Fiş Tarama", "Diğer Uygulamalar", Depts.Accounting, "ScanLine", "#64748b", "Fiş ve faturaların taranarak muhasebeye iletilmesi.",
            [Date("receiptDate", "Fiş tarihi", true), Text("vendor", "Satıcı / mağaza", true), Money("amount", "Tutar (₺)", true),
             Pick("category", "Kategori", true, "Yol", "Yemek", "Kırtasiye", "Konaklama", "Diğer"), File("receiptImage", "Fiş görseli", true)],
            [Owner("Muhasebe Kontrolü")]),
        new("workforce", "İş Gücü Yönetimi", "Diğer Uygulamalar", Depts.Hr, "Users", "#64748b", "Dönemsel ve ek personel ihtiyacı talepleri.",
            [Text("unit", "Mağaza / birim", true), Pick("role", "Pozisyon", true, "Satış Danışmanı", "Kasiyer", "Depo Görevlisi", "Dönemsel Satış Danışmanı"),
             Num("headcount", "Kişi sayısı", true, 1, 100), Date("startDate", "Başlangıç", true), Date("endDate", "Bitiş"),
             Pick("reason", "Neden", true, "Yoğun Sezon", "Kampanya", "Personel Açığı", "Yeni Mağaza")],
            [Manager, Owner("İK Planlama")]),
    ];
}
