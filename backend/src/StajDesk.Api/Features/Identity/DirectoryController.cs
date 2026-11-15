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

[Route("api/directory")]
public class DirectoryController(DirectoryService directory) : ApiController
{
    /// <summary>Çalışan arama (tüm çalışanlar). Yalnızca iş iletişim bilgileri döner.</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] UserQuery q, CancellationToken ct) => Ok(await directory.SearchAsync(q, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await directory.GetAsync(id, ct));

    [HttpGet("birthdays")]
    public async Task<IActionResult> Birthdays([FromQuery] int days = 7, CancellationToken ct = default) => Ok(await directory.BirthdaysAsync(Math.Clamp(days, 0, 31), ct));
}
