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
