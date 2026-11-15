using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public record AssetDto(int Id, string Name, string AssetTag, AssetType Type, AssetStatus Status, string? Manufacturer, string? Model, string? SerialNumber,
    DeptRef? Department, string? Location, UserRef? AssignedTo, DateOnly? PurchaseDate, DateOnly? WarrantyEndsAt, decimal? PurchaseCost, string? Notes,
    DateTime CreatedAt, DateTime UpdatedAt, int OpenTicketCount);

public record AssignmentDto(long Id, UserRef User, UserRef AssignedBy, DateTime AssignedAt, DateTime? ReturnedAt, UserRef? ReturnedBy, string? Note, string? ReturnCondition);

public record InstalledSoftwareDto(long Id, int LicenseId, string Name, string? Version, DateTime InstalledAt);

public record AssetDetailDto(AssetDto Asset, IReadOnlyList<AssignmentDto> Assignments, IReadOnlyList<InstalledSoftwareDto> Software, IReadOnlyList<TicketListItem> Tickets);

public record SaveAssetRequest(string Name, string AssetTag, AssetType Type, AssetStatus Status, string? Manufacturer, string? Model, string? SerialNumber,
    int? DepartmentId, string? Location, DateOnly? PurchaseDate, DateOnly? WarrantyEndsAt, decimal? PurchaseCost, string? Notes);

public record CheckoutRequest(Guid UserId, string? Note);

public record CheckinRequest(string? Condition, string? Note, AssetStatus NewStatus = AssetStatus.InStock);

public record LicenseDto(int Id, string Name, string? Vendor, string? Version, int Seats, int Used, DateOnly? ExpiresAt, decimal? Cost, string? Notes, bool IsExpired);

public record SaveLicenseRequest(string Name, string? Vendor, string? Version, int Seats, DateOnly? ExpiresAt, decimal? Cost, string? Notes);

public record InstallRequest(int AssetId);
