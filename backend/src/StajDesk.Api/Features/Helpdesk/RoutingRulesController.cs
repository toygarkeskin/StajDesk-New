using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

[Route("api/routing-rules")]
[HasPermission(Permissions.TicketsConfigure)]
public class RoutingRulesController(HelpdeskConfigService config) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await config.RulesAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(SaveRoutingRuleRequest req, CancellationToken ct) => Created(await config.SaveRuleAsync(null, req, ct), "Kural eklendi.");

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveRoutingRuleRequest req, CancellationToken ct) => Ok(await config.SaveRuleAsync(id, req, ct), "Kural güncellendi.");

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await config.DeleteRuleAsync(id, ct); return NoContent(); }

    /// <summary>Örnek bir talebin hangi kurala takılacağını gösterir.</summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test(TestRoutingRequest req, CancellationToken ct) => Ok(await config.TestAsync(req, ct));
}
