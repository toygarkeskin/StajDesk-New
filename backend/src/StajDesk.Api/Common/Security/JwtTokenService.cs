using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Common;

public class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var o = options.Value;
        var expires = clock.GetUtcNow().UtcDateTime.AddMinutes(o.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("name", user.FullName),
            new("email", user.Email),
        };
        if (user.DepartmentId is { } d) claims.Add(new Claim("dept", d.ToString()));
        if (user.MustChangePassword) claims.Add(new Claim("mcp", "1"));
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));

        // İçerik base64 ile okunabilir: yalnızca kimlik, rol ve izin kodları taşınır; parola veya kişisel veri asla.
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Issuer, Audience = o.Audience, Expires = expires, Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)), SecurityAlgorithms.HmacSha256),
        });
        return (token, expires);
    }

    /// <summary>Kriptografik rastgele refresh token üretir; veritabanında yalnızca SHA-256 özeti saklanır.</summary>
    public static (string Raw, string Hash) CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (raw, HashToken(raw));
    }

    public static string HashToken(string raw) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
