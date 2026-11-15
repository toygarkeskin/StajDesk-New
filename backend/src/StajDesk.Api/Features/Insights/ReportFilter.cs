using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public class ReportFilter
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? DepartmentId { get; set; }
    public int? CategoryId { get; set; }
    public TicketStatus? Status { get; set; }
    public string? Module { get; set; }
}
