using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;
using StajDesk.Infrastructure.Seed;

namespace StajDesk.Api.Features;

public class TaskQuery : PagedQuery
{
    public int? ProjectId { get; set; }
    public WorkTaskStatus? Status { get; set; }
    /// <summary>mine | created | all</summary>
    public string Scope { get; set; } = "mine";
}
