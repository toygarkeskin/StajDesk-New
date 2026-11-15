using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public class UserQuery : PagedQuery
{
    public int? DepartmentId { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    /// <summary>Belirli çalışanlarla sınırla (rehber).</summary>
    public List<Guid>? Ids { get; set; }
}
