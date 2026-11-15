using System.Net;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Common;

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string? message = null) => new(true, data, message);
    public static ApiResponse<object> Fail(string message, IDictionary<string, string[]>? errors = null, string? code = null) => new(false, null, message, errors, code);
}
