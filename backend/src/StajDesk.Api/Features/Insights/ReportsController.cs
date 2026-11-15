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

[Route("api/reports")]
public class ReportsController(ReportService reports) : ApiController
{
    [HasPermission(Permissions.ReportsView, Permissions.ReportsViewDepartment, Permissions.TicketsViewAll), HttpGet("tickets")]
    public async Task<IActionResult> Tickets([FromQuery] ReportFilter f, CancellationToken ct) => Ok(await reports.TicketsAsync(f, Me, ct));

    [HasPermission(Permissions.ReportsView, Permissions.ReportsViewDepartment, Permissions.TicketsViewAll), HttpGet("tickets/export")]
    public async Task<IActionResult> Export([FromQuery] ReportFilter f, CancellationToken ct) =>
        File(await reports.TicketsCsvAsync(f, Me, ct), "text/csv; charset=utf-8", $"talepler-{DateTime.Now:yyyyMMdd-HHmm}.csv");

    [HasPermission(Permissions.ReportsView, Permissions.ReportsViewDepartment, Permissions.SubmissionsViewAll), HttpGet("workflows")]
    public async Task<IActionResult> Workflows([FromQuery] ReportFilter f, CancellationToken ct) => Ok(await reports.WorkflowsAsync(f, Me, ct));

    [HasPermission(Permissions.InventoryView, Permissions.ReportsView), HttpGet("inventory")]
    public async Task<IActionResult> Inventory(CancellationToken ct) => Ok(await reports.InventoryAsync(ct));

    [HasPermission(Permissions.ReportsView, Permissions.UsersManage, Permissions.LeaveManage), HttpGet("people")]
    public async Task<IActionResult> People(CancellationToken ct) => Ok(await reports.PeopleAsync(ct));
}
