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

/// <summary>Parola doğrulandıktan sonra dönen ikinci adım bilgisi. Kodun kendisi hiçbir yanıtta yer almaz.</summary>
public record LoginChallengeDto(Guid ChallengeId, string MaskedEmail, DateTime ExpiresAt, int ResendAfterSeconds, int CodeLength, string? DevMailboxUrl);

/// <summary>Giriş sonucu: ya doğrudan oturum (kod kapalıysa) ya da doğrulama adımı.</summary>
public record LoginResult(bool RequiresCode, LoginChallengeDto? Challenge, AuthResponse? Auth);

public record VerifyLoginCodeRequest(Guid ChallengeId, string Code);

public record ResendLoginCodeRequest(Guid ChallengeId);
