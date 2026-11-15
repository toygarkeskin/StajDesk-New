using System.Net;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Common;


/// <summary>[HasPermission("tickets.manage")] — izin kodu JWT'deki "perm" talepleriyle sunucu tarafında doğrulanır.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class HasPermissionAttribute(params string[] permissions) : AuthorizeAttribute($"perm:{string.Join('|', permissions)}");
