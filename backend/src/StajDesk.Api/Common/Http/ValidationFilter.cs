using System.Net;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Common;


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
