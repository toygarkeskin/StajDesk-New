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

[Route("api/forms")]
public class FormsController(WorkflowService workflow) : ApiController
{
    /// <summary>Kullanıcının başvurabileceği departman uygulamaları.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool all = false, CancellationToken ct = default) =>
        Ok(await workflow.FormsAsync(Me, all && (Me.Has(Permissions.FormsManage) || Me.IsAdmin), ct));

    [HttpGet("{key}")]
    // edit=true: form tasarımcısı için ham tanım (yalnızca formu yönetebilenler)
    public async Task<IActionResult> Get(string key, [FromQuery] bool edit = false, CancellationToken ct = default) => Ok(await workflow.FormAsync(key, Me, ct, edit));

    [HasPermission(Permissions.FormsManage), HttpPost]
    public async Task<IActionResult> Create(SaveFormRequest req, CancellationToken ct) => Created(await workflow.SaveFormAsync(null, req, Me, ct), "Form oluşturuldu.");

    [HasPermission(Permissions.FormsManage), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveFormRequest req, CancellationToken ct) => Ok(await workflow.SaveFormAsync(id, req, Me, ct), "Form güncellendi.");

    /// <summary>Form başvurusu oluşturur; ilk onay adımı otomatik yönlendirilir.</summary>
    [HttpPost("{key}/submissions")]
    public async Task<IActionResult> Submit(string key, SubmitRequest req, CancellationToken ct)
    {
        var s = await workflow.SubmitAsync(key, req, Me, ct);
        return Created(s, $"{s.Number} numaralı başvurunuz oluşturuldu.");
    }
}
