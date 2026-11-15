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

public record Count(string Key, string Label, int Value, string? Color = null);

public record TrendPoint(DateOnly Date, int Created, int Resolved);

public record DashboardProfile(string FullName, string? JobTitle, string? Department, string? Manager, DateOnly? HireDate, double YearsOfService, IReadOnlyList<string> Roles);

public record SupportWidget(int QueueOpen, int Unassigned, int Overdue, int AssignedToMe, int MyGroups);

public record TicketStatsWidget(string Scope, IReadOnlyList<Count> ByStatus, IReadOnlyList<TrendPoint> Trend, double SlaCompliance, double AvgResolutionHours);

public record TeamWidget(int TeamSize, int OnLeaveToday, int PendingApprovals, IReadOnlyList<UserRef> OnLeave);

public record InventoryWidget(int Total, int InUse, int InStock, int InRepair, int WarrantyExpiring, int LicensesExpiring, int MyAssets);

public record AdminWidget(int PendingAccountRequests, int MustChangePassword, int InactiveUsers, int ActiveUsers);

public record DashboardDto(DashboardProfile Profile, LeaveBalanceDto Leave, int MyOpenTickets, int MyTicketsAwaitingConfirmation, int MyPendingRequests, int ApprovalsPending,
    int AssignedToComplete, int UnreadNotifications, int UnreadMessages, IReadOnlyList<TaskDto> Tasks, IReadOnlyList<AnnouncementDto> Announcements,
    IReadOnlyList<EventDto> Events, IReadOnlyList<BirthdayEntry> Birthdays, IReadOnlyList<AchievementDto> Achievements, IReadOnlyList<OnCallDto> OnCall,
    IReadOnlyList<TicketListItem> RecentTickets, SupportWidget? Support, TicketStatsWidget? TicketStats, TeamWidget? Team, InventoryWidget? Inventory, AdminWidget? Admin, DateTime GeneratedAt);

public record AgentRow(Guid Id, string FullName, int Assigned, int Resolved, double AvgResolutionHours, double SlaCompliance);

public record TicketReport(string Scope, DateOnly From, DateOnly To, int Total, int Open, int Resolved, int Overdue, double AvgResolutionHours, double SlaCompliance,
    IReadOnlyList<Count> ByStatus, IReadOnlyList<Count> ByPriority, IReadOnlyList<Count> ByCategory, IReadOnlyList<Count> ByDepartment, IReadOnlyList<Count> ByGroup,
    IReadOnlyList<TrendPoint> Trend, IReadOnlyList<Count> Aging, IReadOnlyList<AgentRow> Agents);

public record FormRow(string Key, string Name, string Module, int Total, int Pending, int Approved, int Rejected, int Completed, double AvgDecisionHours);

public record WorkflowReport(string Scope, DateOnly From, DateOnly To, int Total, int Pending, int Approved, int Rejected, int Completed, double AvgDecisionHours,
    IReadOnlyList<Count> ByModule, IReadOnlyList<FormRow> ByForm, IReadOnlyList<Count> ByDepartment, IReadOnlyList<TrendPoint> Trend);

public record LicenseUsage(string Name, int Seats, int Used, DateOnly? ExpiresAt);

public record InventoryReport(int Total, decimal TotalValue, IReadOnlyList<Count> ByType, IReadOnlyList<Count> ByStatus, IReadOnlyList<Count> ByDepartment,
    IReadOnlyList<AssetDto> WarrantyExpiring, IReadOnlyList<LicenseUsage> Licenses);

public record PeopleReport(int Active, int Inactive, IReadOnlyList<Count> ByDepartmentType, IReadOnlyList<Count> ByDepartment, IReadOnlyList<Count> ByEmploymentType, int NewHires90, int MustChangePassword);

public record PublicStats(int Employees, int Stores, int Cities, int HeadOfficeDepartments, int ResolvedRequestsThisYear);

public record PublicHome(IReadOnlyList<BannerDto> Banners, IReadOnlyList<AnnouncementDto> News, IReadOnlyList<JobDto> Jobs, IReadOnlyList<EventDto> Events, PublicStats Stats);

public record Rate(string Code, string Name, decimal Value, string Source, string AsOf);

public record MarketData(IReadOnlyList<Rate> Rates, DateTime FetchedAt, IReadOnlyList<string> Errors);

public record WeatherDay(DateOnly Date, double Min, double Max, int Code, string Description);

public record WeatherData(string City, double Temperature, double? Humidity, double? Wind, int Code, string Description, IReadOnlyList<WeatherDay> Days, string Source, DateTime FetchedAt);

public record ComponentHealth(string Name, string Role, bool Healthy, long LatencyMs, string Detail);

public record SystemHealth(bool Healthy, DateTime CheckedAt, IReadOnlyList<ComponentHealth> Components, int OutboxPending, int OutboxFailed);

public record CacheEntry(string Key, double? TtlSeconds);

public record AppEntry(string Key, string Name, string Module, string Icon, string Color, string Route, string Kind, string? Description, int Badge);
