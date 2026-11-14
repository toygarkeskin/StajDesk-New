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

/// <summary>
/// stajdesk.notifications kuyruğunu dinler. Başarılı işlemde ack; ilk hatada bir kez yeniden kuyruğa alır,
/// tekrar hata olursa dead-letter kuyruğuna gönderir. Kuyruk kalıcı olduğu için Worker kapalıyken gelen
/// mesajlar kaybolmaz; Worker açıldığında işlenir.
/// </summary>
public sealed class NotificationConsumer(IOptions<RabbitMqOptions> options, IServiceScopeFactory scopes, ILogger<NotificationConsumer> logger) : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = options.Value.CreateFactory("stajdesk-worker");
        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                _connection = await factory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await MessagingTopology.DeclareAsync(_channel, stoppingToken);
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(30, attempt * 3));
                logger.LogWarning("RabbitMQ'ya bağlanılamadı (deneme {Attempt}), {Delay} sn sonra tekrar: {Error}", attempt, delay.TotalSeconds, ex.Message);
                await Task.Delay(delay, stoppingToken);
            }
        }
        if (_channel is null) return;

        await _channel.BasicQosAsync(0, prefetchCount: 10, global: false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += HandleAsync;
        await _channel.BasicConsumeAsync(MessagingTopology.NotificationQueue, autoAck: false, consumer, stoppingToken);
        logger.LogInformation("Worker '{Queue}' kuyruğunu dinliyor", MessagingTopology.NotificationQueue);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
    }

    private async Task HandleAsync(object sender, BasicDeliverEventArgs ea)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object> { ["RoutingKey"] = ea.RoutingKey, ["MessageId"] = ea.BasicProperties.MessageId ?? "" });
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var router = scope.ServiceProvider.GetRequiredService<NotificationRouter>();
            var json = MessagingTopology.Json;
            var body = ea.Body.Span;
            var count = ea.RoutingKey.Split('.')[0] switch
            {
                "ticket" => await router.HandleAsync(JsonSerializer.Deserialize<TicketEvent>(body, json)!),
                "workflow" => await router.HandleAsync(JsonSerializer.Deserialize<WorkflowEvent>(body, json)!),
                "account" => await router.HandleAsync(JsonSerializer.Deserialize<AccountEvent>(body, json)!),
                "notify" => await router.HandleAsync(JsonSerializer.Deserialize<DirectNotificationEvent>(body, json)!),
                _ => -1,
            };
            if (count < 0) logger.LogWarning("Bilinmeyen olay türü {RoutingKey}, atlandı", ea.RoutingKey);
            else logger.LogInformation("Olay işlendi {RoutingKey}: {Count} bildirim", ea.RoutingKey, count);
            await _channel!.BasicAckAsync(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            var requeue = !ea.Redelivered;
            logger.LogError(ex, "Olay işlenemedi {RoutingKey}; {Action}", ea.RoutingKey, requeue ? "yeniden denenecek" : "dead-letter kuyruğuna gönderildi");
            await _channel!.BasicNackAsync(ea.DeliveryTag, false, requeue);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);
    }
}

public record OutgoingNotification(Guid UserId, string Title, string Message, string Kind, string? Link, Guid EventId);

public interface INotificationChannel
{
    string Name { get; }
    Task SendAsync(IReadOnlyList<OutgoingNotification> notifications, CancellationToken ct);
}

/// <summary>Bildirim merkezine (PostgreSQL) yazar. Aynı olay iki kez gelirse tekrar kayıt oluşturmaz.</summary>
public sealed class InAppChannel(AppDbContext db) : INotificationChannel
{
    public string Name => "in-app";

    public async Task SendAsync(IReadOnlyList<OutgoingNotification> list, CancellationToken ct)
    {
        if (list.Count == 0) return;
        var eventId = list[0].EventId;
        var existing = await db.Notifications.Where(n => n.SourceEventId == eventId).Select(n => n.UserId).ToListAsync(ct);
        foreach (var n in list.Where(n => !existing.Contains(n.UserId)))
            db.Notifications.Add(new Notification { UserId = n.UserId, Title = n.Title, Message = n.Message, Kind = n.Kind, Link = n.Link, SourceEventId = n.EventId });
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>
/// E-posta kanalı (SMTP). Yalnızca aksiyon gerektiren bildirim türleri e-postayla gider.
/// SMTP hatası uygulama içi bildirimi engellemez; hata loglanır (alıcı adresi loglanmaz, yalnızca kullanıcı kimliği).
/// </summary>
public sealed class EmailChannel(AppDbContext db, IEmailSender sender, IOptions<SmtpOptions> options, ILogger<EmailChannel> logger) : INotificationChannel
{
    private static readonly HashSet<string> EmailKinds = ["approval", "ticket", "account", "warning", "success"];
    public string Name => "email";

    public async Task SendAsync(IReadOnlyList<OutgoingNotification> list, CancellationToken ct)
    {
        var targets = list.Where(n => EmailKinds.Contains(n.Kind)).ToList();
        if (targets.Count == 0) return;
        if (!sender.Enabled)
        {
            logger.LogDebug("SMTP tanımlı değil, {Count} e-posta atlandı", targets.Count);
            return;
        }

        var ids = targets.Select(t => t.UserId).ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id) && u.IsActive)
            .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName }).ToDictionaryAsync(u => u.Id, ct);
        var portal = options.Value.PortalUrl.TrimEnd('/');
        var messages = targets.Where(n => users.ContainsKey(n.UserId)).Select(n =>
        {
            var u = users[n.UserId];
            var link = portal + (n.Link ?? "/");
            return new EmailMessage($"{u.FirstName} {u.LastName}", u.Email, $"[StajDesk] {n.Title}",
                $"Merhaba {u.FirstName},\n\n{n.Message}\n\nDetay: {link}\n\nBu e-posta StajDesk tarafından otomatik gönderilmiştir.",
                EmailTemplate.Render(u.FirstName, n.Title, n.Message, link),
                new Dictionary<string, string> { ["X-StajDesk-Event"] = n.EventId.ToString() });
        }).ToList();
        try
        {
            await sender.SendAsync(messages, ct);
            logger.LogInformation("{Count} e-posta gönderildi (kullanıcılar {UserIds})", messages.Count, string.Join(",", targets.Select(t => t.UserId)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // E-posta ikincil kanaldır: hata olayı tekrar işletmez (uygulama içi bildirim zaten kaydedildi).
            logger.LogWarning("E-posta gönderilemedi ({Count} alıcı): {Error}", messages.Count, ex.Message);
        }
    }
}

/// <summary>
/// Gerçek zamanlı kanal: bildirimi Redis pub/sub ile API'ye iletir; API bunu SignalR ile kullanıcının açık oturumlarına gönderir.
/// Redis kapalıysa sessizce atlanır (kullanıcı bildirimi bir sonraki sorguda görür).
/// </summary>
public sealed class RealtimeChannel(StackExchange.Redis.IConnectionMultiplexer redis, ILogger<RealtimeChannel> logger) : INotificationChannel
{
    public const string RedisChannel = "stajdesk:realtime";
    public string Name => "realtime";

    public async Task SendAsync(IReadOnlyList<OutgoingNotification> list, CancellationToken ct)
    {
        if (list.Count == 0) return;
        try
        {
            var sub = redis.GetSubscriber();
            foreach (var n in list)
            {
                var payload = JsonSerializer.Serialize(new RealtimeEnvelope(n.UserId, "notification", new { n.Title, n.Message, n.Kind, n.Link }), MessagingTopology.Json);
                await sub.PublishAsync(StackExchange.Redis.RedisChannel.Literal(RedisChannel), payload);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Gerçek zamanlı bildirim yayınlanamadı: {Error}", ex.Message);
        }
    }
}

public record RealtimeEnvelope(Guid UserId, string Type, object Data);

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
