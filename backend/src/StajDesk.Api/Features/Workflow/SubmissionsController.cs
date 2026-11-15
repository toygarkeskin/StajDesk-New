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

[Route("api/submissions")]
public class SubmissionsController(WorkflowService workflow) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SubmissionQuery q, CancellationToken ct) => Ok(await workflow.ListAsync(q, Me, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await workflow.DetailAsync(id, Me, ct));

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, DecisionRequest req, CancellationToken ct) => Ok(await workflow.DecideAsync(id, true, req, Me, ct), "Onaylandı.");

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, DecisionRequest req, CancellationToken ct) => Ok(await workflow.DecideAsync(id, false, req, Me, ct), "Reddedildi.");

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, DecisionRequest req, CancellationToken ct) => Ok(await workflow.CompleteAsync(id, req, Me, ct), "Başvuru tamamlandı.");

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) => Ok(await workflow.CancelAsync(id, Me, ct), "Başvuru iptal edildi.");

    [HttpPost("{id:int}/comments")]
    public async Task<IActionResult> Comment(int id, DecisionRequest req, CancellationToken ct) => Ok(await workflow.CommentAsync(id, req, Me, ct), "Açıklama eklendi.");
}
