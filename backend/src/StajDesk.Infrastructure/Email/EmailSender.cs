using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace StajDesk.Infrastructure.Email;

public sealed class SmtpOptions
{
    /// <summary>Boşsa e-posta gönderimi kapalıdır.</summary>
    public string Host { get; set; } = "";
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; }
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "StajDesk <noreply@devoplus.local>";
    /// <summary>E-postadaki bağlantılar için portal adresi.</summary>
    public string PortalUrl { get; set; } = "http://localhost:3100";

    public bool Enabled => !string.IsNullOrWhiteSpace(Host);
}

public record EmailMessage(string ToName, string ToAddress, string Subject, string TextBody, string HtmlBody, IReadOnlyDictionary<string, string>? Headers = null);

public interface IEmailSender
{
    bool Enabled { get; }
    /// <summary>Mesajları tek SMTP bağlantısıyla gönderir. Hata durumunda istisna fırlatır (çağıran karar verir).</summary>
    Task SendAsync(IReadOnlyList<EmailMessage> messages, CancellationToken ct);
}

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

/// <summary>Kurumsal e-posta şablonu. Tüm değişken değerler HTML-encode edilir.</summary>
public static class EmailTemplate
{
    private static string E(string s) => System.Net.WebUtility.HtmlEncode(s);

    public static string Render(string firstName, string title, string message, string? link, string? code = null) => $"""
        <!doctype html><html lang="tr"><body style="margin:0;background:#f1f5f9;font-family:Segoe UI,Arial,sans-serif;color:#0f172a">
        <table width="100%" cellpadding="0" cellspacing="0" style="padding:24px 12px"><tr><td align="center">
        <table width="560" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:12px;border:1px solid #e2e8f0">
          <tr><td style="padding:20px 28px;border-bottom:1px solid #e2e8f0;font-weight:700;font-size:16px;color:#d9143f">StajDesk</td></tr>
          <tr><td style="padding:28px">
            <p style="margin:0 0 8px;font-size:14px;color:#475569">Merhaba {E(firstName)},</p>
            <h1 style="margin:0 0 12px;font-size:20px">{E(title)}</h1>
            <p style="margin:0 0 24px;font-size:14px;line-height:1.6;color:#334155">{E(message)}</p>
            {(code is null ? "" : $"""<div style="margin:0 0 24px;padding:16px;border-radius:10px;background:#f8fafc;border:1px solid #e2e8f0;text-align:center;font-size:32px;font-weight:700;letter-spacing:10px;font-family:Consolas,monospace">{E(code)}</div>""")}
            {(link is null ? "" : $"""<a href="{E(link)}" style="display:inline-block;background:#d9143f;color:#ffffff;text-decoration:none;padding:10px 18px;border-radius:8px;font-size:14px;font-weight:600">Portalda aç</a>""")}
          </td></tr>
          <tr><td style="padding:16px 28px;border-top:1px solid #e2e8f0;font-size:12px;color:#94a3b8">Bu e-posta otomatik gönderilmiştir, lütfen yanıtlamayınız.</td></tr>
        </table></td></tr></table></body></html>
        """;
}
