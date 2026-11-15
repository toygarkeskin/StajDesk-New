using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Features;

/// <summary>Standart formlar: ek kural yok.</summary>
public sealed class StandardFormHandler : FormKindHandler
{
    public override IReadOnlyCollection<string> Kinds { get; } = [FormKinds.Standard];
}
