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

[Route("api/leave")]
public class LeaveController(LeaveService leave) : ApiController
{
    [HttpGet("balance")]
    public async Task<IActionResult> MyBalance(CancellationToken ct) => Ok(await leave.MyLeaveAsync(Me.Id, ct));

    [HasPermission(Permissions.LeaveManage), HttpGet("balances")]
    public async Task<IActionResult> Balances([FromQuery] string? search, CancellationToken ct) => Ok(await leave.LeaveBalancesAsync(null, search, ct));

    [HasPermission(Permissions.LeaveManage), HttpPut("balances/{userId:guid}")]
    public async Task<IActionResult> UpdateBalance(Guid userId, UpdateLeaveBalanceRequest req, CancellationToken ct) => Ok(await leave.UpdateLeaveAsync(userId, req, ct), "İzin bakiyesi güncellendi.");

    /// <summary>Kullanıcının departmanındaki izin takvimi.</summary>
    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        return Ok(await leave.LeaveCalendarAsync(Me, start, to ?? start.AddDays(60), ct));
    }
}
