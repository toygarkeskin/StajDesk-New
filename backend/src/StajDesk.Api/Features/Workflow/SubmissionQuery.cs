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

public class SubmissionQuery : PagedQuery
{
    /// <summary>mine | approvals | assigned | history | all</summary>
    public string Scope { get; set; } = "mine";
    public string? FormKey { get; set; }
    public string? Module { get; set; }
    public SubmissionStatus? Status { get; set; }
}
