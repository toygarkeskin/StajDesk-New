using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;

namespace StajDesk.Api.Features;

// Özel form türleri (strateji deseni)
//
// İş akışı motoru (WorkflowService) türden bağımsızdır: alan doğrulama, onay adımları, bildirimler.
// Türe özgü kurallar IFormKindHandler uygulamalarındadır. Yeni bir tür eklemek için motoru değiştirmeye
// gerek yoktur (Açık/Kapalı ilkesi): yeni bir handler yazıp DI'a kaydetmek yeterlidir.

/// <summary>Başvuru gönderilirken türe özgü doğrulamaya verilen bağlam. <see cref="Data"/> normalleştirilmiş form verisidir ve değiştirilebilir.</summary>
public sealed record FormSubmitContext(FormDefinition Form, Dictionary<string, object?> Data, decimal? Amount, UserContext Me);
