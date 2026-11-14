using StajDesk.Api.Common;
using StajDesk.Api.Features;
using StajDesk.Domain;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Tests;

public class TicketServiceTests
{
    private static CreateTicketRequest Valid(int categoryId, string title = "Monitör titriyor", TicketPriority p = TicketPriority.Medium) =>
        new(title, "İkinci monitör birkaç dakikada bir kararıyor.", categoryId, p, TicketType.Incident, null, null);

    [Fact]
    public async Task Create_sets_sla_department_default_group_and_publishes_event()
    {
        var w = new TestWorld();
        var t = await w.Tickets().CreateAsync(Valid(w.Category.Id, p: TicketPriority.High), w.EmployeeCtx);

        Assert.Equal(TicketStatus.Open, t.Status);
        Assert.Equal(TestWorld.Now.AddHours(8), t.DueAt);               // Yüksek öncelik = 8 saat SLA
        Assert.Equal(w.Accounting.Id, t.Department!.Id);                  // talep sahibinin departmanı
        Assert.Equal(w.SupportGroup.Id, t.GroupId);                        // kategorinin varsayılan grubu
        var evt = Assert.IsType<TicketEvent>(Assert.Single(w.Events.Events));
        Assert.Equal("created", evt.Kind);
    }

    [Fact]
    public async Task Create_with_inactive_category_is_rejected()
    {
        var w = new TestWorld();
        w.Category.IsActive = false;
        await w.Db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<AppException>(() => w.Tickets().CreateAsync(Valid(w.Category.Id), w.EmployeeCtx));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, ex.Status);
    }

    [Fact]
    public async Task Routing_rule_assigns_user_and_overrides_priority()
    {
        var w = new TestWorld();
        w.Db.RoutingRules.Add(new RoutingRule { Name = "Kasa kritik", SortOrder = 1, Keyword = "kasa", SetPriority = TicketPriority.Critical, AssignUserId = w.Agent.Id });
        await w.Db.SaveChangesAsync();

        var t = await w.Tickets().CreateAsync(Valid(w.Category.Id, "Mağaza kasası açılmıyor"), w.EmployeeCtx);

        Assert.Equal(TicketPriority.Critical, t.Priority);
        Assert.Equal(TestWorld.Now.AddHours(4), t.DueAt);
        Assert.Equal(w.Agent.Id, t.Assignee!.Id);
        Assert.Equal("Kasa kritik", t.AppliedRule);
    }

    [Fact]
    public async Task Employee_cannot_view_another_employees_ticket()
    {
        var w = new TestWorld();
        var t = await w.Tickets().CreateAsync(Valid(w.Category.Id), TestWorld.Ctx(w.OtherEmployee));
        var ex = await Assert.ThrowsAsync<AppException>(() => w.Tickets().GetAsync(t.Id, w.EmployeeCtx));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, ex.Status);
    }

    [Fact]
    public async Task Manager_sees_department_tickets_but_not_other_departments()
    {
        var w = new TestWorld();
        await w.Tickets().CreateAsync(Valid(w.Category.Id), w.EmployeeCtx);
        await w.Tickets().CreateAsync(Valid(w.Category.Id), TestWorld.Ctx(w.OtherEmployee));

        var page = await w.Tickets().ListAsync(new TicketQuery(), w.ManagerCtx);
        Assert.Single(page.Items);
        Assert.Equal(w.Employee.Id, page.Items[0].Requester.Id);
    }

    [Fact]
    public async Task Requester_cannot_move_open_ticket_to_in_progress()
    {
        var w = new TestWorld();
        var t = await w.Tickets().CreateAsync(Valid(w.Category.Id), w.EmployeeCtx);
        await Assert.ThrowsAsync<AppException>(() => w.Tickets().ChangeStatusAsync(t.Id, new ChangeStatusRequest(TicketStatus.InProgress, null), w.EmployeeCtx));
    }

    [Fact]
    public async Task Full_lifecycle_open_inprogress_resolved_closed()
    {
        var w = new TestWorld();
        var svc = w.Tickets();
        var t = await svc.CreateAsync(Valid(w.Category.Id), w.EmployeeCtx);

        t = await svc.ChangeStatusAsync(t.Id, new ChangeStatusRequest(TicketStatus.InProgress, null), w.AgentCtx);
        Assert.Equal(w.Agent.Id, t.Assignee!.Id); // işleme alan personele otomatik atanır

        t = await svc.ChangeStatusAsync(t.Id, new ChangeStatusRequest(TicketStatus.Resolved, "Kablo değiştirildi."), w.AgentCtx);
        Assert.NotNull(t.ResolvedAt);
        Assert.Contains(TicketStatus.Closed, (await svc.GetAsync(t.Id, w.EmployeeCtx)).AllowedTransitions);

        t = await svc.ChangeStatusAsync(t.Id, new ChangeStatusRequest(TicketStatus.Closed, null), w.EmployeeCtx);
        Assert.Equal(TicketStatus.Closed, t.Status);
        Assert.Empty(t.AllowedTransitions);
    }

    [Fact]
    public async Task Resolving_without_solution_note_is_rejected()
    {
        var w = new TestWorld();
        var t = await w.Tickets().CreateAsync(Valid(w.Category.Id), w.EmployeeCtx);
        var ex = await Assert.ThrowsAsync<AppException>(() => w.Tickets().ChangeStatusAsync(t.Id, new ChangeStatusRequest(TicketStatus.Resolved, null), w.AgentCtx));
        Assert.Contains("çözüm", ex.Message);
    }

    [Fact]
    public async Task Employee_cannot_add_internal_note_and_internal_notes_are_hidden()
    {
        var w = new TestWorld();
        var svc = w.Tickets();
        var t = await svc.CreateAsync(Valid(w.Category.Id), w.EmployeeCtx);
        await Assert.ThrowsAsync<AppException>(() => svc.AddCommentAsync(t.Id, new AddCommentRequest("gizli", true), w.EmployeeCtx));

        await svc.AddCommentAsync(t.Id, new AddCommentRequest("RMA açılacak", true), w.AgentCtx);
        Assert.Empty((await svc.GetAsync(t.Id, w.EmployeeCtx)).Comments);
        Assert.Single((await svc.GetAsync(t.Id, w.AgentCtx)).Comments);
    }

    [Fact]
    public async Task Closed_ticket_rejects_comments()
    {
        var w = new TestWorld();
        var svc = w.Tickets();
        var t = await svc.CreateAsync(Valid(w.Category.Id), w.EmployeeCtx);
        await svc.ChangeStatusAsync(t.Id, new ChangeStatusRequest(TicketStatus.Closed, null), w.AgentCtx);
        await Assert.ThrowsAsync<AppException>(() => svc.AddCommentAsync(t.Id, new AddCommentRequest("tekrar", false), w.EmployeeCtx));
    }
}
