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

[Route("api/dashboard")]
public class DashboardController(DashboardService dashboard) : ApiController
{
    /// <summary>Rol ve yetkiye göre kişiselleştirilmiş özet panel.</summary>
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => Ok(await dashboard.GetAsync(Me, ct));
}
