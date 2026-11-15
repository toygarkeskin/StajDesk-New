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

public record FormSummary(int Id, string Key, string Name, string? Description, string Module, string Icon, string Color, string Kind, DeptRef? OwnerDepartment,
    int StepCount, bool IsActive, int PendingCount);

public record FormDefinitionDto(int Id, string Key, string Name, string? Description, string Module, string Icon, string Color, string Kind, DeptRef? OwnerDepartment,
    IReadOnlyList<FormField> Fields, IReadOnlyList<WorkflowStep> Steps, IReadOnlyList<string> AllowedRoles, bool IsActive, bool CanManage, FormContext? Context);

/// <summary>Özel iş mantıklı formlar için kullanıcıya özel bağlam (izin bakiyesi, limit vb.).</summary>
public record FormContext(decimal? LeaveRemaining, decimal? LeaveEntitled, decimal? LeaveUsed, decimal? LeavePending, decimal? Limit, string? LimitLabel);

public record SaveFormRequest(string Key, string Name, string? Description, string Module, string Icon, string Color, string Kind, int? OwnerDepartmentId,
    List<FormField> Fields, List<WorkflowStep> Steps, List<string>? AllowedRoles, bool IsActive);

public record SubmitRequest(Dictionary<string, JsonElement> Data);

public record DecisionRequest(string? Comment);

public record SubmissionListItem(int Id, string Number, string FormKey, string FormName, string Module, string Icon, string Color, UserRef Submitter, string? DepartmentName,
    SubmissionStatus Status, string? Summary, decimal? Amount, string? CurrentStepName, int CurrentStep, int StepCount, DateTime CreatedAt, DateTime UpdatedAt);

public record SubmissionField(string Key, string Label, string Type, object? Value, string Display);

public record SubmissionActionDto(long Id, SubmissionActionType Action, string? StepName, UserRef? Actor, string? Comment, DateTime CreatedAt);

public record SubmissionStepDto(int Index, string Name, string State, IReadOnlyList<UserRef> Approvers);

public record SubmissionDetail(int Id, string Number, FormSummary Form, UserRef Submitter, string? SubmitterTitle, DeptRef? Department, SubmissionStatus Status,
    string? Summary, decimal? Amount, IReadOnlyList<SubmissionField> Fields, IReadOnlyList<SubmissionStepDto> Steps, IReadOnlyList<SubmissionActionDto> Actions,
    IReadOnlyList<FileDto> Files, UserRef? Assignee, bool CanApprove, bool CanComplete, bool CanCancel, DateTime CreatedAt, DateTime UpdatedAt, DateTime? CompletedAt);

public record LeaveBalanceDto(Guid UserId, string FullName, string? Department, int Year, decimal Entitled, decimal CarriedOver, decimal Used, decimal Pending, decimal Remaining);

public record UpdateLeaveBalanceRequest(decimal Entitled, decimal CarriedOver);

public record LeaveCalendarEntry(int SubmissionId, UserRef User, string LeaveType, DateOnly Start, DateOnly End, int Days, SubmissionStatus Status);

/// <summary>Form verisinde "kişi" alanının saklanan değeri.</summary>
public record FieldUserValue(Guid Id, string Name);

public record RevenueDay(DateOnly Date, decimal Amount);

public record RevenueStore(string Store, decimal Amount, int Reports, int Receipts, decimal? AverageBasket);

public record RevenueSummary(DateOnly From, DateOnly To, bool CompanyWide, decimal Total, int Reports, int Receipts, decimal? AverageBasket, int PendingCount,
    IReadOnlyList<RevenueDay> ByDay, IReadOnlyList<RevenueStore> ByStore);

public record ShiftChangeEntry(int SubmissionId, UserRef User, DateOnly Date, string? From, string? To, string? SwapWith, SubmissionStatus Status);

public record LimitDto(int Id, string Kind, string Label, decimal MaxAmount, string Currency);

public record UpdateLimitRequest(decimal MaxAmount);
