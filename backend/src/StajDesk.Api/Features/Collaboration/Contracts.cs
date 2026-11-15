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

public record AnnouncementDto(int Id, string Title, string Summary, string Body, AnnouncementKind Kind, bool IsPublic, bool IsPinned, DateTime PublishAt, DateTime? ExpiresAt, DeptRef? Department, UserRef Author);

public record SaveAnnouncementRequest(string Title, string Summary, string Body, AnnouncementKind Kind, bool IsPublic, bool IsPinned, DateTime? PublishAt, DateTime? ExpiresAt, int? DepartmentId, bool Notify);

public record BannerDto(int Id, string Title, string? Subtitle, string? ImageUrl, string Theme, string? LinkUrl, string? LinkText, int SortOrder, DateTime? StartsAt, DateTime? EndsAt, bool IsActive, bool IsLive);

public record SaveBannerRequest(string Title, string? Subtitle, string? ImageUrl, string Theme, string? LinkUrl, string? LinkText, int SortOrder, DateTime? StartsAt, DateTime? EndsAt, bool IsActive);

public record ReorderRequest(List<int> Ids);

public record EventDto(int Id, string Title, string? Description, string? Location, string Category, DateTime StartsAt, DateTime? EndsAt, bool IsPublic, int AttendeeCount, bool IsAttending, UserRef CreatedBy);

public record SaveEventRequest(string Title, string? Description, string? Location, string Category, DateTime StartsAt, DateTime? EndsAt, bool IsPublic);

public record JobDto(int Id, string Title, DeptRef? Department, string? Location, EmploymentType EmploymentType, string Description, string? Requirements, bool IsPublic, bool IsActive, DateOnly? ClosesAt, DateTime CreatedAt);

public record SaveJobRequest(string Title, int? DepartmentId, string? Location, EmploymentType EmploymentType, string Description, string? Requirements, bool IsPublic, bool IsActive, DateOnly? ClosesAt);

public record AchievementDto(int Id, UserRef User, string? UserTitle, string Title, string? Description, AchievementKind Kind, UserRef GivenBy, int Likes, DateTime CreatedAt);

public record GiveKudosRequest(Guid UserId, string Title, string? Description, AchievementKind Kind);

public record SurveyListItem(int Id, string Title, string? Description, bool IsAnonymous, bool IsActive, DateTime? EndsAt, int QuestionCount, int ResponseCount, bool HasResponded);

public record SurveyDto(int Id, string Title, string? Description, bool IsAnonymous, bool IsActive, DateTime? EndsAt, IReadOnlyList<SurveyQuestion> Questions, bool HasResponded, bool CanManage);

public record SaveSurveyRequest(string Title, string? Description, bool IsAnonymous, bool IsActive, DateTime? EndsAt, List<SurveyQuestion> Questions);

public record SurveyAnswerRequest(Dictionary<string, JsonElement> Answers);

public record QuestionResult(string Id, string Text, string Type, IReadOnlyDictionary<string, int> Counts, double? Average, IReadOnlyList<string> TextAnswers);

public record SurveyResults(int SurveyId, string Title, int ResponseCount, IReadOnlyList<QuestionResult> Questions);

public record DocumentDto(int Id, string Title, string? Description, string Category, FileDto File, DeptRef? Department, UserRef UploadedBy, int Downloads, DateTime CreatedAt);

public record SaveDocumentRequest(string Title, string? Description, string Category, Guid FileId, int? DepartmentId);

public record ConversationDto(UserRef User, string? JobTitle, string LastMessage, DateTime LastAt, int Unread, bool LastFromMe);

public record MessageDto(long Id, Guid SenderId, string Body, DateTime CreatedAt, DateTime? ReadAt);

public record SendMessageRequest(Guid RecipientId, string Body);

public record ProjectDto(int Id, string Code, string Name, string? Description, DeptRef? Department, UserRef Owner, ProjectStatus Status, DateOnly? StartDate, DateOnly? DueDate, int TaskCount, int DoneCount);

public record SaveProjectRequest(string Code, string Name, string? Description, int? DepartmentId, Guid OwnerId, ProjectStatus Status, DateOnly? StartDate, DateOnly? DueDate);

public record TaskDto(int Id, int? ProjectId, string? ProjectName, string Title, string? Description, UserRef? Assignee, UserRef CreatedBy, WorkTaskStatus Status, TicketPriority Priority, DateOnly? DueDate, bool IsOverdue, DateTime? CompletedAt, DateTime CreatedAt);

public record SaveTaskRequest(int? ProjectId, string Title, string? Description, Guid? AssigneeId, WorkTaskStatus Status, TicketPriority Priority, DateOnly? DueDate);

public record MoveTaskRequest(WorkTaskStatus Status);

public record StoreDto(int Id, string Code, string Name, string? City, DeptRef? Department, UserRef? Manager, string? Phone, string? Address, StoreStatus Status, decimal? AreaSqm, int StaffCount);

public record SaveStoreRequest(string Code, string Name, string? City, Guid? ManagerId, string? Phone, string? Address, StoreStatus Status, decimal? AreaSqm);

public record OnCallDto(int Id, UserRef User, string? Phone, string? JobTitle, string Team, DateTime StartsAt, DateTime EndsAt, string? Note, bool IsCurrent);

public record SaveOnCallRequest(Guid UserId, string Team, DateTime StartsAt, DateTime EndsAt, string? Note);

public record NotificationDto(long Id, string Title, string Message, string Kind, string? Link, bool IsRead, DateTime CreatedAt);
