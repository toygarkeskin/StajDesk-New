using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace StajDesk.Infrastructure.Email;

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
