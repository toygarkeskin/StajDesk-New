using System.Net;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Common;

/// <summary>İlk girişte parola belirlemeyen kullanıcı yalnızca kimlik uçlarına erişebilir.</summary>
public class PasswordChangeGateMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.User.Identity?.IsAuthenticated == true && ctx.User.HasClaim("mcp", "1")
            && !ctx.Request.Path.StartsWithSegments("/api/auth"))
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(ApiResponse.Fail("Devam etmeden önce yeni parolanızı belirlemeniz gerekmektedir.", code: "PASSWORD_CHANGE_REQUIRED"));
            return;
        }
        await next(ctx);
    }
}
