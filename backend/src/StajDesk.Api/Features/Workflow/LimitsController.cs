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

[Route("api/limits")]
public class LimitsController(SpendingLimitService limits) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await limits.LimitsAsync(ct));

    [HasPermission(Permissions.LimitsManage), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateLimitRequest req, CancellationToken ct) => Ok(await limits.UpdateLimitAsync(id, req, ct), "Limit güncellendi.");
}
