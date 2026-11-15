using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

[Route("api/kb")]
public class KnowledgeController(KnowledgeService kb) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ArticleQuery q, CancellationToken ct) => Ok(await kb.ListAsync(q, Me, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, [FromQuery] bool view = true, CancellationToken ct = default) => Ok(await kb.GetAsync(id, Me, view, ct));

    [HasPermission(Permissions.KnowledgeManage), HttpPost]
    public async Task<IActionResult> Create(SaveArticleRequest req, CancellationToken ct) => Created(await kb.SaveAsync(null, req, Me, ct), "Makale yayınlandı.");

    [HasPermission(Permissions.KnowledgeManage), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveArticleRequest req, CancellationToken ct) => Ok(await kb.SaveAsync(id, req, Me, ct), "Makale güncellendi.");

    [HttpPost("{id:int}/helpful")]
    public async Task<IActionResult> Helpful(int id, CancellationToken ct) { await kb.HelpfulAsync(id, ct); return NoContent(); }

    [HasPermission(Permissions.KnowledgeManage), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) { await kb.DeleteAsync(id, ct); return NoContent(); }
}
