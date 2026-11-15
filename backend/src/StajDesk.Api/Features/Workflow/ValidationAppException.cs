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

/// <summary>Alan bazlı doğrulama hataları (400) — standart yanıt zarfının "errors" alanına yazılır.</summary>
public class ValidationAppException(Dictionary<string, string[]> errors) : AppException(System.Net.HttpStatusCode.BadRequest, "Gönderilen bilgilerde hata var. Lütfen alanları kontrol ediniz.", "VALIDATION")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}
