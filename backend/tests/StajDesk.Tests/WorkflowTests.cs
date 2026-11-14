using System.Text.Json;
using StajDesk.Api.Common;
using StajDesk.Api.Features;
using StajDesk.Domain;

namespace StajDesk.Tests;

public class DomainRuleTests
{
    [Theory]
    [InlineData(TicketStatus.Open, TicketStatus.InProgress, true)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Resolved, true)]
    [InlineData(TicketStatus.Resolved, TicketStatus.Closed, true)]
    [InlineData(TicketStatus.Closed, TicketStatus.Open, false)]
    [InlineData(TicketStatus.Pending, TicketStatus.Closed, false)]
    public void Ticket_transitions_follow_lifecycle(TicketStatus from, TicketStatus to, bool expected) =>
        Assert.Equal(expected, TicketRules.CanTransition(from, to));

    [Fact]
    public void Working_days_skip_weekends_and_public_holidays()
    {
        // 26-30 Ekim 2026: 29 Ekim Cumhuriyet Bayramı (Perşembe) → 4 iş günü
        Assert.Equal(4, LeaveRules.WorkingDays(new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 30)));
        // Cumartesi-Pazar
        Assert.Equal(0, LeaveRules.WorkingDays(new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 15)));
    }

    [Theory]
    [InlineData(-200, 0)]
    [InlineData(-400, 14)]
    [InlineData(-6 * 365, 20)]
    [InlineData(-16 * 365, 26)]
    public void Annual_entitlement_depends_on_seniority(int daysAgo, int expected)
    {
        var today = new DateOnly(2026, 11, 16);
        Assert.Equal(expected, LeaveRules.AnnualEntitlement(today.AddDays(daysAgo), today));
    }

    [Fact]
    public void Routing_rule_matches_keyword_case_insensitively()
    {
        var rule = new RoutingRule { Name = "VPN", Keyword = "vpn", MinPriority = TicketPriority.Medium };
        Assert.True(rule.Matches(1, null, "VPN kopuyor", "", TicketPriority.High));
        Assert.False(rule.Matches(1, null, "VPN kopuyor", "", TicketPriority.Low));
        Assert.False(rule.Matches(1, null, "Yazıcı", "toner", TicketPriority.High));
    }

    [Fact]
    public void Password_policy_requires_length_upper_lower_digit()
    {
        Assert.False(PasswordPolicy.IsValid("kisa1A"));
        Assert.False(PasswordPolicy.IsValid("tamamenkucuk123"));
        Assert.True(PasswordPolicy.IsValid("Guclu.Parola2026"));
        Assert.True(PasswordPolicy.IsValid(PasswordPolicy.Generate()));
    }
}

public class WorkflowServiceTests
{
    private static Dictionary<string, JsonElement> Data(object o) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(o))!;

    private static FormDefinition LeaveForm(TestWorld w) => new()
    {
        Key = "leave", Name = "İzin", Module = "İK", Kind = FormKinds.Leave, OwnerDepartment = w.It,
        Fields =
        [
            new FormField { Key = "leaveType", Label = "İzin türü", Type = FieldTypes.Select, Required = true, Options = ["Yıllık İzin", "Mazeret İzni"] },
            new FormField { Key = "startDate", Label = "Başlangıç", Type = FieldTypes.Date, Required = true },
            new FormField { Key = "endDate", Label = "Bitiş", Type = FieldTypes.Date, Required = true },
        ],
        Steps = [new WorkflowStep { Name = "Yönetici Onayı", ApproverType = ApproverType.Manager }],
    };

    [Fact]
    public async Task Leave_is_routed_to_manager_and_deducted_from_balance_after_approval()
    {
        var w = new TestWorld();
        w.Db.FormDefinitions.Add(LeaveForm(w));
        w.Db.LeaveBalances.Add(new LeaveBalance { UserId = w.Employee.Id, Year = 2026, Entitled = 14 });
        await w.Db.SaveChangesAsync();
        var svc = w.Workflow();

        var s = await svc.SubmitAsync("leave", new SubmitRequest(Data(new { leaveType = "Yıllık İzin", startDate = "2026-11-23", endDate = "2026-11-27" })), w.EmployeeCtx);
        Assert.Equal(SubmissionStatus.Pending, s.Status);
        Assert.Contains("5 iş günü", s.Summary);
        Assert.Equal(w.Manager.Id, Assert.Single(s.Steps[0].Approvers).Id);

        s = await svc.DecideAsync(s.Id, true, new DecisionRequest("Uygundur"), TestWorld.Ctx(w.Manager));
        Assert.Equal(SubmissionStatus.Approved, s.Status);
        Assert.Equal(5, (await w.Leave().MyLeaveAsync(w.Employee.Id, default)).Used);
    }

    [Fact]
    public async Task Leave_exceeding_balance_is_rejected()
    {
        var w = new TestWorld();
        w.Db.FormDefinitions.Add(LeaveForm(w));
        w.Db.LeaveBalances.Add(new LeaveBalance { UserId = w.Employee.Id, Year = 2026, Entitled = 2 });
        await w.Db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<AppException>(() => w.Workflow().SubmitAsync("leave",
            new SubmitRequest(Data(new { leaveType = "Yıllık İzin", startDate = "2026-11-23", endDate = "2026-11-27" })), w.EmployeeCtx));
        Assert.Contains("bakiyeniz yetersiz", ex.Message);
    }

    [Fact]
    public async Task Required_fields_are_validated_on_server()
    {
        var w = new TestWorld();
        w.Db.FormDefinitions.Add(LeaveForm(w));
        await w.Db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<ValidationAppException>(() => w.Workflow().SubmitAsync("leave", new SubmitRequest(Data(new { leaveType = "Uydurma" })), w.EmployeeCtx));
        Assert.Contains("leaveType", ex.Errors.Keys);
        Assert.Contains("startDate", ex.Errors.Keys);
    }

    [Fact]
    public async Task Only_pending_approver_can_decide()
    {
        var w = new TestWorld();
        w.Db.FormDefinitions.Add(LeaveForm(w));
        w.Db.LeaveBalances.Add(new LeaveBalance { UserId = w.Employee.Id, Year = 2026, Entitled = 14 });
        await w.Db.SaveChangesAsync();
        var s = await w.Workflow().SubmitAsync("leave", new SubmitRequest(Data(new { leaveType = "Mazeret İzni", startDate = "2026-11-23", endDate = "2026-11-23" })), w.EmployeeCtx);
        await Assert.ThrowsAsync<AppException>(() => w.Workflow().DecideAsync(s.Id, true, new DecisionRequest(null), TestWorld.Ctx(w.Agent)));
    }

    [Fact]
    public async Task Step_is_skipped_when_approver_is_the_submitter()
    {
        var w = new TestWorld();
        var form = LeaveForm(w);
        form.Key = "self";
        form.Kind = FormKinds.Standard;
        form.Steps = [new WorkflowStep { Name = "Departman yöneticisi", ApproverType = ApproverType.DepartmentManager }];
        w.Db.FormDefinitions.Add(form);
        w.Manager.ManagerId = null;
        await w.Db.SaveChangesAsync();
        // Muhasebe departman yöneticisi kendisi başvuru yaparsa adım atlanır; onaylayıcı bulunamazsa Admin'e düşer
        var s = await w.Workflow().SubmitAsync("self", new SubmitRequest(Data(new { leaveType = "Mazeret İzni", startDate = "2026-11-23", endDate = "2026-11-23" })), TestWorld.Ctx(w.Manager));
        Assert.Equal(SubmissionStatus.Approved, s.Status);
        Assert.Contains(s.Actions, a => a.Action == SubmissionActionType.Skipped);
    }
}
