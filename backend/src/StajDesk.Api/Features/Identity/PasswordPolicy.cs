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

public static class PasswordPolicy
{
    public const string Message = "Parola en az 10 karakter olmalı; büyük harf, küçük harf ve rakam içermelidir.";
    public static bool IsValid(string? p) => p is { Length: >= 10 } && p.Any(char.IsUpper) && p.Any(char.IsLower) && p.Any(char.IsDigit);

    public static string Generate()
    {
        const string upper = "ABCDEFGHJKLMNPRSTUVYZ", lower = "abcdefghijkmnprstuvyz", digits = "23456789", symbols = "!@#$%";
        var chars = new[] { Pick(upper), Pick(upper), Pick(lower), Pick(lower), Pick(lower), Pick(lower), Pick(digits), Pick(digits), Pick(digits), Pick(symbols), Pick(lower), Pick(upper) };
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
        static char Pick(string s) => s[RandomNumberGenerator.GetInt32(s.Length)];
    }
}
