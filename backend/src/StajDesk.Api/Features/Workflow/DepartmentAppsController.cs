using System.Globalization;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

[Route("api")]
public class DepartmentAppsController(RevenueService revenue, ShiftService shifts) : ApiController
{
    /// <summary>Mağaza ciro özeti (son N gün). Yetkiye göre şirket geneli veya yalnızca kendi bildirimleri.</summary>
    [HttpGet("revenue/summary")]
    public async Task<IActionResult> Revenue([FromQuery] int days = 30, CancellationToken ct = default) => Ok(await revenue.SummaryAsync(Me, days, ct));

    /// <summary>Vardiya değişiklikleri (varsayılan: bugünden itibaren 30 gün).</summary>
    [HttpGet("shifts")]
    public async Task<IActionResult> Shifts([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(await shifts.ChangesAsync(Me, start, to ?? start.AddDays(30), ct));
    }
}
