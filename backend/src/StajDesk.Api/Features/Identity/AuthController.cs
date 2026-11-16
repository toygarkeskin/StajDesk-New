using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

[Route("api/auth")]
public class AuthController(AuthService auth, IOptions<JwtOptions> jwt, IWebHostEnvironment env, IConfiguration config) : ApiController
{
    private const string Cookie = "sd_rt";

    /// <summary>
    /// Yenileme çerezi HTTPS'te her zaman Secure'dur. Şifresiz HTTP'de Secure çerez tarayıcıda yalnızca localhost için
    /// saklanır; telefondan ağ adresiyle denerken oturum her yenilemede düşerdi. Bu yüzden HTTP geliştirme kurulumunda
    /// Auth:SecureCookie=false verilir, canlı katmanda (HTTPS) true zorunludur.
    /// </summary>
    private bool SecureCookie => Request.IsHttps || config.GetValue("Auth:SecureCookie", !env.IsDevelopment());

    private void SetCookie(string token) => Response.Cookies.Append(Cookie, token, new CookieOptions
    {
        HttpOnly = true, Secure = SecureCookie, SameSite = SameSiteMode.Strict, Path = "/api/auth",
        Expires = DateTimeOffset.UtcNow.AddDays(jwt.Value.RefreshTokenDays),
    });

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// 1. adım: e-posta ve parola. Doğrulama kodu açıksa <c>requiresCode=true</c> ve meydan okuma bilgisi döner (kod e-postayla gönderilir);
    /// kapalıysa oturum bilgisi döner ve refresh token httpOnly çerezle yazılır.
    /// </summary>
    [AllowAnonymous, EnableRateLimiting("public"), HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct)
    {
        var (res, refresh) = await auth.LoginAsync(req, Ip, ct);
        if (refresh is not null) SetCookie(refresh);
        return Ok(res, res.RequiresCode ? "Doğrulama kodu e-posta adresinize gönderildi." : null);
    }

    /// <summary>2. adım: e-postaya gelen doğrulama kodu. Başarılıysa oturum açılır.</summary>
    [AllowAnonymous, EnableRateLimiting("public"), HttpPost("login/verify")]
    public async Task<IActionResult> VerifyCode(VerifyLoginCodeRequest req, CancellationToken ct)
    {
        var (res, refresh) = await auth.VerifyLoginCodeAsync(req, Ip, ct);
        SetCookie(refresh);
        return Ok(res);
    }

    /// <summary>Yeni doğrulama kodu (bekleme süresi ve gönderim sınırı uygulanır).</summary>
    [AllowAnonymous, EnableRateLimiting("public"), HttpPost("login/resend")]
    public async Task<IActionResult> ResendCode(ResendLoginCodeRequest req, CancellationToken ct) =>
        Ok(await auth.ResendLoginCodeAsync(req.ChallengeId, ct), "Yeni doğrulama kodu gönderildi.");

    /// <summary>Refresh token ile yeni erişim token'ı (token rotasyonu uygulanır).</summary>
    [AllowAnonymous, HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var (res, refresh) = await auth.RefreshAsync(Request.Cookies[Cookie], Ip, ct);
        SetCookie(refresh);
        return Ok(res);
    }

    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[Cookie], ct);
        Response.Cookies.Delete(Cookie, new CookieOptions { Path = "/api/auth" });
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> MeInfo(CancellationToken ct) => Ok(await auth.MeAsync(Me.Id, ct));

    /// <summary>Parola değiştirme. İlk girişte zorunludur.</summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
    {
        var (res, refresh) = await auth.ChangePasswordAsync(Me, req, Ip, ct);
        SetCookie(refresh);
        return Ok(res, "Parolanız güncellendi.");
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest req, CancellationToken ct) => Ok(await auth.UpdateProfileAsync(Me, req, ct), "Profiliniz güncellendi.");
}
