using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

[Route("api/system")]
public class SystemController(SystemService system) : ApiController
{
    [HasPermission(Permissions.SystemManage), HttpGet("health")] public async Task<IActionResult> Health(CancellationToken ct) => Ok(await system.HealthAsync(ct));
    [HasPermission(Permissions.SystemManage), HttpGet("cache")] public async Task<IActionResult> Cache() => Ok(await system.CacheAsync());
    [HasPermission(Permissions.SystemManage), HttpDelete("cache")] public async Task<IActionResult> ClearCache([FromQuery] string? prefix) => Ok(new { removed = await system.ClearCacheAsync(prefix) }, "Önbellek temizlendi.");
}
