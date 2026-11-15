using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public record UserRef(Guid Id, string FullName, string Email);

public record FileDto(Guid Id, string FileName, string ContentType, long Size, DateTime CreatedAt);

public record TicketListItem(int Id, string Title, TicketStatus Status, TicketPriority Priority, TicketType Type, int CategoryId, string CategoryName, string CategoryColor,
    UserRef Requester, string? DepartmentName, UserRef? Assignee, string? GroupName, DateTime CreatedAt, DateTime UpdatedAt, DateTime DueAt, bool IsOverdue, int CommentCount, int AttachmentCount);

public record TicketComment(int Id, string Body, bool IsInternal, UserRef Author, bool AuthorIsStaff, DateTime CreatedAt);

public record TicketHistoryDto(long Id, string Field, string? OldValue, string? NewValue, UserRef? Actor, DateTime CreatedAt);

public record AssetRef(int Id, string Name, string AssetTag, AssetType Type);

public record TicketDetail(int Id, string Title, string Description, TicketStatus Status, TicketPriority Priority, TicketType Type,
    int CategoryId, string CategoryName, string CategoryColor, UserRef Requester, string? RequesterTitle, string? RequesterPhone, DeptRef? Department,
    int? GroupId, string? GroupName, UserRef? Assignee, AssetRef? Asset, string? AppliedRule,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime DueAt, DateTime? ResolvedAt, DateTime? ClosedAt, bool IsOverdue,
    IReadOnlyList<TicketStatus> AllowedTransitions, bool CanEdit, bool CanManage,
    IReadOnlyList<TicketComment> Comments, IReadOnlyList<TicketHistoryDto> History, IReadOnlyList<FileDto> Attachments);

public record CreateTicketRequest(string Title, string Description, int CategoryId, TicketPriority Priority, TicketType Type, int? AssetId, List<Guid>? AttachmentIds);

public record UpdateTicketRequest(string Title, string Description, int CategoryId, TicketPriority Priority, TicketType Type, int? AssetId);

public record ChangeStatusRequest(TicketStatus Status, string? Note);

public record AssignTicketRequest(int? GroupId, Guid? AssigneeId);

public record AddCommentRequest(string Body, bool IsInternal);

public record CategoryDto(int Id, string Name, string? Description, string Color, bool IsActive, int? DefaultGroupId, string? DefaultGroupName, int TicketCount, int OpenTicketCount);

public record SaveCategoryRequest(string Name, string? Description, string Color, bool IsActive, int? DefaultGroupId);

public record RoutingRuleDto(int Id, string Name, int SortOrder, bool IsActive, int? CategoryId, string? CategoryName, int? DepartmentId, string? DepartmentName,
    string? Keyword, TicketPriority? MinPriority, int? AssignGroupId, string? AssignGroupName, Guid? AssignUserId, string? AssignUserName, TicketPriority? SetPriority, int MatchCount);

public record SaveRoutingRuleRequest(string Name, int SortOrder, bool IsActive, int? CategoryId, int? DepartmentId, string? Keyword, TicketPriority? MinPriority,
    int? AssignGroupId, Guid? AssignUserId, TicketPriority? SetPriority);

public record TestRoutingRequest(int CategoryId, int? DepartmentId, string Title, string Description, TicketPriority Priority);

public record ArticleListItem(int Id, string Title, string Summary, int? CategoryId, string? CategoryName, string? CategoryColor, bool IsFaq, UserRef Author, int Views, int HelpfulCount, bool IsPublished, DateTime UpdatedAt);

public record ArticleDto(int Id, string Title, string Summary, string Content, int? CategoryId, string? CategoryName, string? CategoryColor, bool IsFaq, UserRef Author, int Views, int HelpfulCount, bool IsPublished, DateTime CreatedAt, DateTime UpdatedAt);

public record SaveArticleRequest(string Title, string Summary, string Content, int? CategoryId, bool IsFaq, bool IsPublished = true);
