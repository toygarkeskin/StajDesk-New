using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public class AppCatalogService(WorkflowService workflow)
{
    private record ModuleApp(string Key, string Name, string Module, string Icon, string Color, string Route, string? Description, string[]? AnyPermission = null);

    private static readonly ModuleApp[] Modules =
    [
        new("agent-management", "Agent Management", "IT", "Headset", "#2563eb", "/admin/groups", "Destek grupları ve ekip üyeleri.", [Permissions.OrgManage, Permissions.TicketsConfigure]),
        new("cache-clear", "Cache Clear", "IT", "Eraser", "#2563eb", "/system/cache", "Redis önbellek anahtarlarını görüntüleme ve temizleme.", [Permissions.SystemManage]),
        new("support-ticket", "Support Ticket", "IT", "LifeBuoy", "#2563eb", "/tickets/new", "BT destek talebi oluşturma."),
        new("limits", "Limitler", "Muhasebe", "Gauge", "#dc2626", "/apps/limits", "Avans ve masraf üst limitleri."),
        new("stores", "Mağaza Yönetimi", "Diğer Uygulamalar", "Store", "#64748b", "/stores", "Mağaza listesi, yönetici ve durum bilgileri."),
        new("on-call", "Nöbetçi Kişi", "Diğer Uygulamalar", "PhoneCall", "#64748b", "/on-call", "Mesai dışı nöbetçi personel çizelgesi."),
        new("faq", "SSS", "Diğer Uygulamalar", "CircleHelp", "#64748b", "/kb?faq=1", "Sık sorulan sorular."),
        new("tasks", "Görevler", "Diğer Uygulamalar", "ListChecks", "#64748b", "/tasks", "Kişisel ve proje görevleri (kanban)."),
        new("directory", "Çalışan Arama", "Diğer Uygulamalar", "Search", "#64748b", "/directory", "Çalışan rehberi."),
        new("notifications", "Bildirim Merkezi", "Diğer Uygulamalar", "Bell", "#64748b", "/notifications", "Tüm bildirimleriniz."),
        new("messages", "Mesajlaşma", "Diğer Uygulamalar", "MessagesSquare", "#64748b", "/messages", "Çalışanlar arası mesajlaşma."),
        new("documents", "Dokümanlar", "Diğer Uygulamalar", "FolderOpen", "#64748b", "/documents", "Politika, yönetmelik ve şablonlar."),
        new("reports", "Raporlama", "Diğer Uygulamalar", "ChartColumn", "#64748b", "/reports", "Talep, iş akışı ve envanter raporları.", [Permissions.ReportsView, Permissions.ReportsViewDepartment, Permissions.TicketsViewAll]),
        new("surveys", "Anketler", "Diğer Uygulamalar", "ClipboardList", "#64748b", "/surveys", "Çalışan anketleri."),
        new("jobs", "İş İlanları", "Diğer Uygulamalar", "Briefcase", "#64748b", "/jobs", "Açık pozisyonlar ve iç ilanlar."),
        new("events", "Etkinlikler", "Diğer Uygulamalar", "CalendarHeart", "#64748b", "/events", "Kurum içi etkinlikler."),
        new("achievements", "Çalışan Başarıları", "Diğer Uygulamalar", "Trophy", "#64748b", "/achievements", "Ödüller ve takdirler."),
        new("projects", "Projeler", "Strateji ve Dönüşüm Ofisi", "FolderKanban", "#0f766e", "/projects", "Proje portföyü ve ilerleme."),
    ];

    public async Task<List<AppEntry>> ListAsync(UserContext me, CancellationToken ct)
    {
        var forms = await workflow.FormsAsync(me, false, ct);
        var list = forms.Select(f => new AppEntry(f.Key, f.Name, f.Module, f.Icon, f.Color, $"/apps/{f.Key}", "form", f.Description, f.PendingCount)).ToList();
        list.AddRange(Modules.Where(m => m.AnyPermission is null || m.AnyPermission.Any(me.Has))
            .Select(m => new AppEntry(m.Key, m.Name, m.Module, m.Icon, m.Color, m.Route, "module", m.Description, 0)));
        string[] order = ["IT", "Genel Süreçler", "İnsan Kaynakları ve İdari İşler", "Kalite", "Lojistik", "Mağaza Muhasebesi", "Muhasebe", "Pazarlama", "Satın Alma", "Ürün", "Yönetim", "Strateji ve Dönüşüm Ofisi", "Yurtdışı Satış", "Yurtiçi Satış", "Diğer Uygulamalar"];
        return list.OrderBy(a => Array.IndexOf(order, a.Module) is var i && i < 0 ? 99 : i).ThenBy(a => a.Kind == "module").ThenBy(a => a.Name, StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), true)).ToList();
    }
}
