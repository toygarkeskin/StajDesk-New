using System.Net;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Common;


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
