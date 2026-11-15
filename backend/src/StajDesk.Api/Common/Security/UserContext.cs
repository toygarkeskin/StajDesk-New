using System.Net;
using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Common;


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
