using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Features;

public interface IFormKindHandler
{
    /// <summary>Bu işleyicinin sorumlu olduğu form türleri (<see cref="FormKinds"/>).</summary>
    IReadOnlyCollection<string> Kinds { get; }

    /// <summary>Türe özgü kuralları uygular; geçersizse istisna fırlatır. Özel bir özet üretirse döner, yoksa null.</summary>
    Task<string?> ValidateAsync(FormSubmitContext ctx, CancellationToken ct);

    /// <summary>Tüm onay adımları tamamlanınca çalışır (ör. izin bakiyesinden düşme).</summary>
    Task OnApprovedAsync(FormSubmission submission, Dictionary<string, JsonElement> data, CancellationToken ct);

    /// <summary>Başvuru ekranında kullanıcıya gösterilecek bağlam (bakiye, limit vb.).</summary>
    Task<FormContext?> UserContextAsync(FormDefinition form, Guid userId, CancellationToken ct);

    /// <summary>Form alanlarını istemciye göndermeden önce zenginleştirir (ör. seçenekleri veritabanından doldurma).</summary>
    Task<IReadOnlyList<FormField>> PrepareFieldsAsync(FormDefinition form, CancellationToken ct);

    /// <summary>Başvuru detayında gösterilecek hesaplanmış ek alanlar.</summary>
    IEnumerable<SubmissionField> ExtraFields(Dictionary<string, JsonElement> data);
}
