using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace StajDesk.Infrastructure.Email;

public record EmailMessage(string ToName, string ToAddress, string Subject, string TextBody, string HtmlBody, IReadOnlyDictionary<string, string>? Headers = null);
