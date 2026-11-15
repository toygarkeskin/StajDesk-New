using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace StajDesk.Infrastructure.Email;

/// <summary>MailKit tabanlı SMTP gönderici. Alıcı adresleri ve içerik loglanmaz.</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public bool Enabled => options.Value.Enabled;

    public async Task SendAsync(IReadOnlyList<EmailMessage> messages, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.Enabled || messages.Count == 0) return;
        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(o.Host, o.Port, o.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, ct);
        if (!string.IsNullOrEmpty(o.User)) await smtp.AuthenticateAsync(o.User, o.Password, ct);
        foreach (var m in messages)
        {
            var msg = new MimeMessage();
            msg.From.Add(MailboxAddress.Parse(o.From));
            msg.To.Add(new MailboxAddress(m.ToName, m.ToAddress));
            msg.Subject = m.Subject;
            if (m.Headers is not null) foreach (var (k, v) in m.Headers) msg.Headers.Add(k, v);
            msg.Body = new BodyBuilder { TextBody = m.TextBody, HtmlBody = m.HtmlBody }.ToMessageBody();
            await smtp.SendAsync(new MailKit.SendRequest(msg), ct);
        }
        await smtp.DisconnectAsync(true, ct);
        logger.LogDebug("{Count} e-posta SMTP ile gönderildi", messages.Count);
    }
}
