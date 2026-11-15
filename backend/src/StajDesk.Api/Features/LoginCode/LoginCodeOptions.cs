using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Email;

namespace StajDesk.Api.Features;

public sealed class LoginCodeOptions
{
    /// <summary>Girişte e-posta doğrulama kodu istensin mi? SMTP tanımlı değilse uygulanamaz ve kapalı kabul edilir.</summary>
    public bool Enabled { get; set; } = true;
    public int CodeLength { get; set; } = 6;
    public int ValidMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    public int MaxSends { get; set; } = 4;
    public int ResendCooldownSeconds { get; set; } = 30;
    /// <summary>Yalnızca geliştirme: giriş ekranında gösterilecek test posta kutusu adresi (ör. Mailpit). Kod asla API yanıtında dönmez.</summary>
    public string? DevMailboxUrl { get; set; }
}
