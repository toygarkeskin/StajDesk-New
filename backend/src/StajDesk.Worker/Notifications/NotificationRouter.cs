using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Email;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Worker;

/// <summary>Olayları alıcılara ve mesajlara çevirir, tüm kanallara iletir.</summary>
public sealed class NotificationRouter(AppDbContext db, IEnumerable<INotificationChannel> channels)
{
    private async Task<List<Guid>> SupportStaffAsync(int? groupId)
    {
        var groupMembers = groupId is { } g ? await db.GroupMembers.Where(m => m.GroupId == g && m.User.IsActive).Select(m => m.UserId).ToListAsync() : [];
        return groupMembers.Count > 0 ? groupMembers
            : await db.Users.Where(u => u.IsActive && u.Roles.Any(r => r.Role.Code == RoleCodes.It)).Select(u => u.Id).ToListAsync();
    }

    private static string Status(string? s) => Enum.TryParse<TicketStatus>(s, out var st) ? TicketRules.Label(st) : s ?? "";

    public async Task<int> HandleAsync(TicketEvent e)
    {
        var link = $"/tickets/{e.TicketId}";
        var list = new List<OutgoingNotification>();
        void Add(Guid to, string title, string msg, string kind) { if (to != e.ActorId) list.Add(new(to, title, msg, kind, link, e.EventId)); }
        switch (e.Kind)
        {
            case "created":
                // Talep sahibine alındı onayı (aktör kendisi olsa da gönderilir)
                list.Add(new(e.RequesterId, "Talebiniz alındı", $"#{e.TicketId} \"{e.Title}\" talebiniz destek ekibine iletildi.", "info", link, e.EventId));
                if (e.AssigneeId is { } a) Add(a, "Size yeni talep atandı", $"#{e.TicketId} {e.Title}", "ticket");
                foreach (var s in await SupportStaffAsync(e.GroupId)) if (s != e.AssigneeId) Add(s, e.Priority == "Critical" ? "Kritik talep!" : "Yeni talep", $"{e.ActorName}: #{e.TicketId} {e.Title}", "ticket");
                break;
            case "status_changed":
                var kind = e.NewValue is "Resolved" or "Closed" ? "success" : e.NewValue == "Pending" ? "warning" : "info";
                Add(e.RequesterId, $"Talebiniz: {Status(e.NewValue)}", $"#{e.TicketId} \"{e.Title}\" {Status(e.OldValue)} → {Status(e.NewValue)}", kind);
                if (e.AssigneeId is { } asg) Add(asg, $"Talep durumu: {Status(e.NewValue)}", $"#{e.TicketId} \"{e.Title}\"", kind);
                break;
            case "assigned":
                if (e.AssigneeId is { } assignee) Add(assignee, "Size talep atandı", $"#{e.TicketId} \"{e.Title}\"", "ticket");
                if (e.NewValue is not null) Add(e.RequesterId, "Talebiniz işleme alındı", $"#{e.TicketId} talebiniz {e.NewValue} tarafından takip ediliyor.", "info");
                break;
            case "commented":
                if (!e.IsInternal) Add(e.RequesterId, "Talebinize yanıt geldi", $"{e.ActorName}, #{e.TicketId} \"{e.Title}\" talebine yanıt yazdı.", "ticket");
                if (e.AssigneeId is { } ag) Add(ag, e.IsInternal ? "Yeni iç not" : "Talepte yeni yorum", $"{e.ActorName} · #{e.TicketId} {e.Title}", "ticket");
                else if (e.ActorId == e.RequesterId) foreach (var s in await SupportStaffAsync(e.GroupId)) Add(s, "Talepte yeni yorum", $"{e.ActorName} · #{e.TicketId} {e.Title}", "ticket");
                break;
        }
        return await DispatchAsync(list);
    }

    public async Task<int> HandleAsync(WorkflowEvent e)
    {
        var link = $"/submissions/{e.SubmissionId}";
        var (title, msg, kind) = e.Kind switch
        {
            "step_pending" => ("Onayınız bekleniyor", $"{e.Number} · {e.FormName} ({e.StepName})", "approval"),
            "approved" => ("Başvurunuz onaylandı", $"{e.Number} · {e.FormName} tüm onay adımlarını tamamladı.", "success"),
            "rejected" => ("Başvurunuz reddedildi", $"{e.Number} · {e.FormName}: {e.Comment}", "warning"),
            "completed" => ("Başvurunuz tamamlandı", $"{e.Number} · {e.FormName} sorumlu birim tarafından tamamlandı.", "success"),
            "cancelled" => ("Başvuru iptal edildi", $"{e.Number} · {e.FormName} başvuru sahibi tarafından iptal edildi.", "info"),
            "commented" => ("Başvuruya açıklama eklendi", $"{e.ActorName} · {e.Number}: {e.Comment}", "info"),
            _ => ("Başvuru güncellendi", $"{e.Number} · {e.FormName}", "info"),
        };
        var list = e.RecipientIds.Distinct().Where(r => r != e.ActorId || e.Kind == "approved")
            .Select(r => new OutgoingNotification(r, r == e.SubmitterId || e.Kind != "approved" ? title : "Tamamlanacak başvuru", msg, kind, e.Kind == "step_pending" ? "/approvals" : link, e.EventId)).ToList();
        return await DispatchAsync(list);
    }

    public async Task<int> HandleAsync(AccountEvent e)
    {
        var (title, msg, link) = e.Kind switch
        {
            "request_created" => ("Yeni hesap talebi", $"İK, {e.FullName} için hesap açılmasını talep etti.", "/admin/account-requests"),
            "account_created" => ("Hesap oluşturuldu", $"{e.FullName} için hesap oluşturuldu. Geçici parola IT tarafından iletilecektir.", "/admin/account-requests"),
            "account_deactivated" => ("Hesap devre dışı bırakıldı", $"{e.FullName} hesabı devre dışı. Zimmetli cihazların iadesini kontrol ediniz.", "/inventory"),
            _ => ("Hesap bildirimi", e.FullName, "/"),
        };
        return await DispatchAsync(e.RecipientIds.Distinct().Where(r => r != e.ActorId).Select(r => new OutgoingNotification(r, title, msg, "account", link, e.EventId)).ToList());
    }

    public Task<int> HandleAsync(DirectNotificationEvent e) =>
        DispatchAsync(e.RecipientIds.Distinct().Select(r => new OutgoingNotification(r, e.Title, e.Message, e.Kind, e.Link, e.EventId)).ToList());

    private async Task<int> DispatchAsync(List<OutgoingNotification> list)
    {
        list = list.DistinctBy(n => n.UserId).ToList();
        foreach (var channel in channels) await channel.SendAsync(list, CancellationToken.None);
        return list.Count;
    }
}
