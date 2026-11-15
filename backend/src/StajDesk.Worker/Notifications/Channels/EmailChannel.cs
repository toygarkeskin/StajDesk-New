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
