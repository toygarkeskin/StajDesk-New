using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public class TicketQuery : PagedQuery
{
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
    public int? CategoryId { get; set; }
    public int? DepartmentId { get; set; }
    public int? GroupId { get; set; }
    public Guid? AssigneeId { get; set; }
    public int? AssetId { get; set; }
    /// <summary>mine | assigned | mygroups | unassigned | overdue | active | department</summary>
    public string? Scope { get; set; }
    /// <summary>newest | oldest | priority | due | updated</summary>
    public string? Sort { get; set; }
}
