using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public class AssetQuery : PagedQuery
{
    public AssetType? Type { get; set; }
    public AssetStatus? Status { get; set; }
    public int? DepartmentId { get; set; }
    public Guid? AssignedToId { get; set; }
    public bool? WarrantyExpiring { get; set; }
    public bool Mine { get; set; }
}
