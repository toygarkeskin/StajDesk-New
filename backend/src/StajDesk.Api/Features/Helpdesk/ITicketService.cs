using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public interface ITicketService
{
    Task<PagedResult<TicketListItem>> ListAsync(TicketQuery q, UserContext me, CancellationToken ct = default);
    Task<TicketDetail> GetAsync(int id, UserContext me, CancellationToken ct = default);
    Task<TicketDetail> CreateAsync(CreateTicketRequest req, UserContext me, CancellationToken ct = default);
    Task<TicketDetail> UpdateAsync(int id, UpdateTicketRequest req, UserContext me, CancellationToken ct = default);
    Task<TicketDetail> ChangeStatusAsync(int id, ChangeStatusRequest req, UserContext me, CancellationToken ct = default);
    Task<TicketDetail> AssignAsync(int id, AssignTicketRequest req, UserContext me, CancellationToken ct = default);
    Task<TicketComment> AddCommentAsync(int id, AddCommentRequest req, UserContext me, CancellationToken ct = default);
    Task<IReadOnlyList<FileDto>> AttachAsync(int id, IReadOnlyList<Guid> fileIds, UserContext me, CancellationToken ct = default);
    Task DeleteAsync(int id, UserContext me, CancellationToken ct = default);
    Task<Expression<Func<Ticket, bool>>> VisibilityAsync(UserContext me, CancellationToken ct = default);
}
