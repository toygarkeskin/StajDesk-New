using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Features;

/// <summary>Varsayılan (etkisiz) davranışlar; işleyiciler yalnızca ihtiyaç duydukları adımları ezer.</summary>
public abstract class FormKindHandler : IFormKindHandler
{
    public abstract IReadOnlyCollection<string> Kinds { get; }
    public virtual Task<string?> ValidateAsync(FormSubmitContext ctx, CancellationToken ct) => Task.FromResult<string?>(null);
    public virtual Task OnApprovedAsync(FormSubmission submission, Dictionary<string, JsonElement> data, CancellationToken ct) => Task.CompletedTask;
    public virtual Task<FormContext?> UserContextAsync(FormDefinition form, Guid userId, CancellationToken ct) => Task.FromResult<FormContext?>(null);
    public virtual Task<IReadOnlyList<FormField>> PrepareFieldsAsync(FormDefinition form, CancellationToken ct) => Task.FromResult<IReadOnlyList<FormField>>(form.Fields);
    public virtual IEnumerable<SubmissionField> ExtraFields(Dictionary<string, JsonElement> data) => [];
}
