using System.Net;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Common;

// ======================= Yanıt zarfı =======================

/// <summary>Tüm uçların döndüğü standart yanıt: { success, data, message, errors }.</summary>
public record ApiResponse<T>(bool Success, T? Data, string? Message = null, IDictionary<string, string[]>? Errors = null, string? Code = null);

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string? message = null) => new(true, data, message);
    public static ApiResponse<object> Fail(string message, IDictionary<string, string[]>? errors = null, string? code = null) => new(false, null, message, errors, code);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class PagedQuery
{
    private int _page = 1, _pageSize = 20;
    public int Page { get => _page; set => _page = Math.Max(1, value); }
    public int PageSize { get => _pageSize; set => _pageSize = Math.Clamp(value, 1, 200); }
    public string? Search { get; set; }
}

public static class QueryExtensions
{
    public static async Task<PagedResult<TOut>> ToPagedAsync<TIn, TOut>(this IQueryable<TIn> q, PagedQuery p, System.Linq.Expressions.Expression<Func<TIn, TOut>> map, CancellationToken ct)
    {
        var total = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(q, ct);
        var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(q.Skip((p.Page - 1) * p.PageSize).Take(p.PageSize).Select(map), ct);
        return new PagedResult<TOut>(items, p.Page, p.PageSize, total);
    }

    public static string Like(this string term) => $"%{term.Trim().Replace("%", "\\%").Replace("_", "\\_")}%";
}

// ======================= Hatalar =======================

public class AppException(HttpStatusCode status, string message, string? code = null) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
    public string? Code { get; } = code;
    public int? RetryAfterSeconds { get; init; }

    public static AppException NotFound(string what) => new(HttpStatusCode.NotFound, $"{what} bulunamadı.");
    public static AppException Forbidden(string? msg = null) => new(HttpStatusCode.Forbidden, msg ?? "Bu işlem için yetkiniz bulunmamaktadır.");
    public static AppException BadRequest(string msg) => new(HttpStatusCode.BadRequest, msg);
    public static AppException Conflict(string msg) => new(HttpStatusCode.Conflict, msg);
    public static AppException Unauthorized(string msg) => new(HttpStatusCode.Unauthorized, msg);
}

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

// ======================= Kimlik bağlamı =======================

/// <summary>İsteği yapan kullanıcı; servisler HttpContext'e değil bu kayda bağımlıdır (birim test edilebilirlik).</summary>
public record UserContext(Guid Id, string Name, int? DepartmentId, IReadOnlySet<string> Roles, IReadOnlySet<string> Permissions)
{
    public bool Has(string permission) => Permissions.Contains(permission);
    public bool HasRole(string role) => Roles.Contains(role);
    public bool IsAdmin => Roles.Contains(RoleCodes.SuperAdmin) || Roles.Contains(RoleCodes.Admin);

    public static UserContext From(ClaimsPrincipal p)
    {
        var id = p.FindFirstValue("sub") ?? throw AppException.Unauthorized("Oturum bilgisi okunamadı.");
        return new UserContext(Guid.Parse(id), p.FindFirstValue("name") ?? "",
            int.TryParse(p.FindFirstValue("dept"), out var d) ? d : null,
            p.FindAll("role").Select(c => c.Value).ToHashSet(),
            p.FindAll("perm").Select(c => c.Value).ToHashSet());
    }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue("sub"), out var id) ? id : null;
    public string? UserName => accessor.HttpContext?.User.FindFirstValue("name");
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}

// ======================= Yetkilendirme =======================

/// <summary>[HasPermission("tickets.manage")] — izin kodu JWT'deki "perm" talepleriyle sunucu tarafında doğrulanır.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class HasPermissionAttribute(params string[] permissions) : AuthorizeAttribute($"perm:{string.Join('|', permissions)}");

public sealed class PermissionRequirement(string[] anyOf) : IAuthorizationRequirement
{
    public string[] AnyOf { get; } = anyOf;
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement req)
    {
        if (req.AnyOf.Any(p => context.User.HasClaim("perm", p))) context.Succeed(req);
        return Task.CompletedTask;
    }
}

public sealed class PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith("perm:")) return await base.GetPolicyAsync(policyName);
        return new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[5..].Split('|'))).Build();
    }
}

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

// ======================= Doğrulama =======================

/// <summary>Action parametreleri için kayıtlı FluentValidation doğrulayıcılarını çalıştırır.</summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(arg.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;
            var result = await validator.ValidateAsync(new ValidationContext<object>(arg), context.HttpContext.RequestAborted);
            foreach (var g in result.Errors.GroupBy(e => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName.Split('.').Last())))
                errors[g.Key] = g.Select(e => e.ErrorMessage).Distinct().ToArray();
        }
        if (errors.Count > 0)
        {
            context.Result = new BadRequestObjectResult(ApiResponse.Fail("Gönderilen bilgilerde hata var. Lütfen alanları kontrol ediniz.", errors));
            return;
        }
        await next();
    }
}

[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiController : ControllerBase
{
    protected UserContext Me => UserContext.From(User);
    protected OkObjectResult Ok<T>(T data, string? message = null) => base.Ok(ApiResponse.Ok(data, message));
    protected ObjectResult Created<T>(T data, string? message = null) => StatusCode(201, ApiResponse.Ok(data, message));
}
