using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Features;

/// <summary>Türe göre işleyici seçer. Kaydı olmayan tür standart davranışa düşer.</summary>
public sealed class FormKindRegistry
{
    private readonly Dictionary<string, IFormKindHandler> _handlers;
    private static readonly StandardFormHandler Standard = new();

    public FormKindRegistry(IEnumerable<IFormKindHandler> handlers)
    {
        _handlers = new(StringComparer.OrdinalIgnoreCase);
        foreach (var h in handlers)
            foreach (var k in h.Kinds)
                if (!_handlers.TryAdd(k, h)) throw new InvalidOperationException($"'{k}' form türü için birden fazla işleyici kayıtlı.");
    }

    public IFormKindHandler For(string kind) => _handlers.GetValueOrDefault(kind) ?? Standard;
}
