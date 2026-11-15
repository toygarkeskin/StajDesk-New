using System.Net;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Common;

[ApiController]
[Authorize]
[Produces("application/json")]
public abstract class ApiController : ControllerBase
{
    protected UserContext Me => UserContext.From(User);
    protected OkObjectResult Ok<T>(T data, string? message = null) => base.Ok(ApiResponse.Ok(data, message));
    protected ObjectResult Created<T>(T data, string? message = null) => StatusCode(201, ApiResponse.Ok(data, message));
}
