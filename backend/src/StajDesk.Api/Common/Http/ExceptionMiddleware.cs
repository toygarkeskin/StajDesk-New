using System.Net;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Common;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (AppException ex)
        {
            logger.LogInformation("İstek reddedildi {Status} {Path}: {Message}", (int)ex.Status, ctx.Request.Path, ex.Message);
            if (ex.RetryAfterSeconds is { } s) ctx.Response.Headers.RetryAfter = s.ToString();
            await Write(ctx, ex.Status, ex.Message, ex.Code, (ex as Features.ValidationAppException)?.Errors);
        }
        catch (DomainException ex)
        {
            await Write(ctx, HttpStatusCode.BadRequest, ex.Message, null);
        }
        catch (Exception ex) when (ex.InnerException is System.Text.DecoderFallbackException || ex is System.Text.Json.JsonException)
        {
            await Write(ctx, HttpStatusCode.BadRequest, "İstek gövdesi geçerli bir UTF-8 JSON değil.", "INVALID_JSON");
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // istemci bağlantıyı kapattı
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Beklenmeyen hata {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            await Write(ctx, HttpStatusCode.InternalServerError, "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyiniz.", null);
        }
    }

    private static Task Write(HttpContext ctx, HttpStatusCode status, string message, string? code, IDictionary<string, string[]>? errors = null)
    {
        if (ctx.Response.HasStarted) return Task.CompletedTask;
        ctx.Response.StatusCode = (int)status;
        return ctx.Response.WriteAsJsonAsync(ApiResponse.Fail(message, errors, code));
    }
}
