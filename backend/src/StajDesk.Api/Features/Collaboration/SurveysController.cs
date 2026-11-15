using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;
using StajDesk.Infrastructure.Seed;

namespace StajDesk.Api.Features;

[Route("api/surveys")]
public class SurveysController(CollaborationService svc) : ApiController
{
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await svc.SurveysAsync(Me, ct));
    [HttpGet("{id:int}")] public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await svc.SurveyAsync(id, Me, ct));
    [HasPermission(Permissions.SurveysManage), HttpPost] public async Task<IActionResult> Create(SaveSurveyRequest req, CancellationToken ct) => Created(await svc.SaveSurveyAsync(null, req, Me, ct), "Anket yayınlandı.");
    [HasPermission(Permissions.SurveysManage), HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, SaveSurveyRequest req, CancellationToken ct) => Ok(await svc.SaveSurveyAsync(id, req, Me, ct), "Anket güncellendi.");
    [HttpPost("{id:int}/responses")] public async Task<IActionResult> Respond(int id, SurveyAnswerRequest req, CancellationToken ct) { await svc.RespondAsync(id, req, Me, ct); return Ok(true, "Yanıtınız kaydedildi. Teşekkür ederiz."); }
    [HasPermission(Permissions.SurveysManage), HttpGet("{id:int}/results")] public async Task<IActionResult> Results(int id, CancellationToken ct) => Ok(await svc.ResultsAsync(id, ct));
}
