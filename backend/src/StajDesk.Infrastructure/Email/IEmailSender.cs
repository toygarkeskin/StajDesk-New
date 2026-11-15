using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace StajDesk.Infrastructure.Email;

public interface IEmailSender
{
    bool Enabled { get; }
    /// <summary>Mesajları tek SMTP bağlantısıyla gönderir. Hata durumunda istisna fırlatır (çağıran karar verir).</summary>
    Task SendAsync(IReadOnlyList<EmailMessage> messages, CancellationToken ct);
}
