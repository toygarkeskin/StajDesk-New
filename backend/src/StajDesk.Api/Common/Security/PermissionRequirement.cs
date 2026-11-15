using System.Net;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Common;

public sealed class PermissionRequirement(string[] anyOf) : IAuthorizationRequirement
{
    public string[] AnyOf { get; } = anyOf;
}
