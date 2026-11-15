using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Features;

/// <summary>İş akışı verisi için ortak yardımcılar.</summary>
public static class WorkflowData
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    /// <summary>"Yaşayan" başvurular: reddedilen ve iptal edilenler hariç.</summary>
    public static readonly SubmissionStatus[] LiveStatuses = [SubmissionStatus.Pending, SubmissionStatus.Approved, SubmissionStatus.Completed];

    public static Dictionary<string, JsonElement> Parse(string json) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, Json) ?? [];
    public static decimal? Dec(Dictionary<string, object?> data, string key) => data.GetValueOrDefault(key) is decimal d ? d : null;
    public static DateOnly? DateOf(Dictionary<string, JsonElement> d, string key) =>
        d.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String && DateOnly.TryParse(v.GetString(), CultureInfo.InvariantCulture, out var x) ? x : null;
}
