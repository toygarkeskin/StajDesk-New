using System.Text.Json;
using StajDesk.Api.Common;
using StajDesk.Api.Features;
using StajDesk.Domain;

namespace StajDesk.Tests;

/// <summary>Özel iş mantıklı departman uygulamaları: Ciro ve Vardiya.</summary>
public class DepartmentAppTests
{
    private static Dictionary<string, JsonElement> Data(object o) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(o))!;

    // ---------------- Ciro ----------------
    private static async Task<TestWorld> RevenueWorld()
    {
        var w = new TestWorld();
        w.Db.Stores.AddRange(
            new Store { Code = "M001", Name = "Kadıköy Mağazası", Status = StoreStatus.Open },
            new Store { Code = "M002", Name = "Eski Mağaza", Status = StoreStatus.Closed });
        w.Db.FormDefinitions.Add(new FormDefinition
        {
            Key = "store-revenue", Name = "Ciro", Module = "Mağaza Muhasebesi", Kind = FormKinds.Revenue, OwnerDepartment = w.Accounting,
            Fields =
            [
                new FormField { Key = "storeName", Label = "Mağaza", Type = FieldTypes.Text, Required = true },
                new FormField { Key = "date", Label = "Tarih", Type = FieldTypes.Date, Required = true },
                new FormField { Key = "cashAmount", Label = "Nakit", Type = FieldTypes.Currency, Min = 0 },
                new FormField { Key = "cardAmount", Label = "Kart", Type = FieldTypes.Currency, Min = 0 },
                new FormField { Key = "amount", Label = "Toplam", Type = FieldTypes.Currency, Required = true, Min = 0 },
                new FormField { Key = "receiptCount", Label = "Fiş", Type = FieldTypes.Number, Min = 0 },
            ],
            Steps = [new WorkflowStep { Name = "Kontrol", ApproverType = ApproverType.ResponsibleDepartment }],
        });
        await w.Db.SaveChangesAsync();
        return w;
    }

    [Fact]
    public async Task Revenue_calculates_average_basket_and_blocks_duplicate_for_same_store_and_day()
    {
        var w = await RevenueWorld();
        var svc = w.Workflow();
        var s = await svc.SubmitAsync("store-revenue", new SubmitRequest(Data(new { storeName = "kadıköy mağazası", date = "2026-11-15", cashAmount = 4000, cardAmount = 6000, amount = 10000, receiptCount = 40 })), w.EmployeeCtx);

        Assert.Contains("Kadıköy Mağazası", s.Summary);       // mağaza adı kayıttaki biçime normalleştirilir
        Assert.Contains("40 fiş", s.Summary);
        Assert.Contains("250,00", s.Summary);                  // ortalama sepet 10000 / 40

        var dup = await Assert.ThrowsAsync<AppException>(() => svc.SubmitAsync("store-revenue",
            new SubmitRequest(Data(new { storeName = "Kadıköy Mağazası", date = "2026-11-15", amount = 500 })), TestWorld.Ctx(w.OtherEmployee)));
        Assert.Contains("zaten var", dup.Message);
    }

    [Fact]
    public async Task Revenue_total_must_match_cash_plus_card()
    {
        var w = await RevenueWorld();
        var ex = await Assert.ThrowsAsync<ValidationAppException>(() => w.Workflow().SubmitAsync("store-revenue",
            new SubmitRequest(Data(new { storeName = "Kadıköy Mağazası", date = "2026-11-15", cashAmount = 100, cardAmount = 200, amount = 500 })), w.EmployeeCtx));
        Assert.Contains("amount", ex.Errors.Keys);
    }

    [Theory]
    [InlineData("Eski Mağaza", "2026-11-15", "storeName")]      // kapalı mağaza
    [InlineData("Olmayan Mağaza", "2026-11-15", "storeName")]
    [InlineData("Kadıköy Mağazası", "2026-11-20", "date")]      // ileri tarih
    [InlineData("Kadıköy Mağazası", "2026-09-01", "date")]      // 45 günden eski
    public async Task Revenue_rejects_invalid_store_or_date(string store, string date, string field)
    {
        var w = await RevenueWorld();
        var ex = await Assert.ThrowsAsync<ValidationAppException>(() => w.Workflow().SubmitAsync("store-revenue",
            new SubmitRequest(Data(new { storeName = store, date, amount = 1000 })), w.EmployeeCtx));
        Assert.Contains(field, ex.Errors.Keys);
    }

    [Fact]
    public async Task Revenue_summary_counts_only_approved_reports_and_scopes_by_permission()
    {
        var w = await RevenueWorld();
        var svc = w.Workflow();
        var a = await svc.SubmitAsync("store-revenue", new SubmitRequest(Data(new { storeName = "Kadıköy Mağazası", date = "2026-11-14", amount = 8000, receiptCount = 20 })), TestWorld.Ctx(w.OtherEmployee));
        await svc.SubmitAsync("store-revenue", new SubmitRequest(Data(new { storeName = "Kadıköy Mağazası", date = "2026-11-15", amount = 3000 })), TestWorld.Ctx(w.OtherEmployee));
        await svc.DecideAsync(a.Id, true, new DecisionRequest(null), TestWorld.Ctx(w.Manager));

        // Sorumlu birim (Muhasebe) üyesi şirket genelini görür
        var all = await w.Revenue().SummaryAsync(w.EmployeeCtx, 30, default);
        Assert.True(all.CompanyWide);
        Assert.Equal(8000, all.Total);
        Assert.Equal(1, all.PendingCount);
        Assert.Equal(400, all.AverageBasket);
        Assert.Equal(30, all.ByDay.Count);

        // Başka departmandan, rapor yetkisi olmayan çalışan yalnızca kendi bildirimlerini görür
        var own = await w.Revenue().SummaryAsync(TestWorld.Ctx(w.Agent), 30, default);
        Assert.False(own.CompanyWide);
        Assert.Equal(0, own.Total);
    }

    // ---------------- Vardiya ----------------
    private static async Task<(TestWorld W, User Colleague)> ShiftWorld()
    {
        var w = new TestWorld();
        var colleague = new User { FirstName = "Can", LastName = "Ekip", Email = "can@test", Department = w.Accounting, Manager = w.Manager, MustChangePassword = false };
        w.Db.Users.Add(colleague);
        string[] shifts = ["Sabah", "Akşam", "İzin"];
        w.Db.FormDefinitions.Add(new FormDefinition
        {
            Key = "shift", Name = "Vardiya", Module = "İK", Kind = FormKinds.Shift, OwnerDepartment = w.It,
            Fields =
            [
                new FormField { Key = "date", Label = "Tarih", Type = FieldTypes.Date, Required = true },
                new FormField { Key = "currentShift", Label = "Mevcut", Type = FieldTypes.Select, Required = true, Options = [.. shifts] },
                new FormField { Key = "requestedShift", Label = "İstenen", Type = FieldTypes.Select, Required = true, Options = [.. shifts] },
                new FormField { Key = "swapWith", Label = "Takas", Type = FieldTypes.User },
            ],
            Steps =
            [
                new WorkflowStep { Name = "Takas Onayı", ApproverType = ApproverType.FieldUser, FieldKey = "swapWith" },
                new WorkflowStep { Name = "Yönetici Onayı", ApproverType = ApproverType.Manager },
            ],
        });
        await w.Db.SaveChangesAsync();
        return (w, colleague);
    }

    [Fact]
    public async Task Shift_swap_goes_to_colleague_first_then_manager()
    {
        var (w, colleague) = await ShiftWorld();
        var svc = w.Workflow();
        var s = await svc.SubmitAsync("shift", new SubmitRequest(Data(new { date = "2026-11-20", currentShift = "Sabah", requestedShift = "Akşam", swapWith = colleague.Id.ToString() })), w.EmployeeCtx);

        Assert.Equal(colleague.Id, Assert.Single(s.Steps[0].Approvers).Id);
        Assert.Contains("takas: Can Ekip", s.Summary);
        Assert.Equal("Can Ekip", s.Fields.Single(f => f.Key == "swapWith").Display);

        s = await svc.DecideAsync(s.Id, true, new DecisionRequest("Olur"), TestWorld.Ctx(colleague));
        Assert.Equal(SubmissionStatus.Pending, s.Status);
        Assert.Equal(w.Manager.Id, Assert.Single(s.Steps[1].Approvers).Id);

        var team = await w.Shifts().ChangesAsync(TestWorld.Ctx(colleague), new DateOnly(2026, 11, 16), new DateOnly(2026, 12, 16), default);
        Assert.Equal("Can Ekip", Assert.Single(team).SwapWith);
    }

    [Fact]
    public async Task Shift_without_swap_skips_colleague_step()
    {
        var (w, _) = await ShiftWorld();
        var s = await w.Workflow().SubmitAsync("shift", new SubmitRequest(Data(new { date = "2026-11-20", currentShift = "Sabah", requestedShift = "Akşam" })), w.EmployeeCtx);
        Assert.Equal("skipped", s.Steps[0].State, ignoreCase: true);
        Assert.Equal(w.Manager.Id, Assert.Single(s.Steps[1].Approvers).Id);
    }

    [Fact]
    public async Task Shift_rules_reject_same_shift_past_date_other_department_and_duplicates()
    {
        var (w, _) = await ShiftWorld();
        var svc = w.Workflow();

        var same = await Assert.ThrowsAsync<ValidationAppException>(() => svc.SubmitAsync("shift",
            new SubmitRequest(Data(new { date = "2026-11-20", currentShift = "Sabah", requestedShift = "Sabah" })), w.EmployeeCtx));
        Assert.Contains("requestedShift", same.Errors.Keys);

        var past = await Assert.ThrowsAsync<ValidationAppException>(() => svc.SubmitAsync("shift",
            new SubmitRequest(Data(new { date = "2026-11-10", currentShift = "Sabah", requestedShift = "Akşam" })), w.EmployeeCtx));
        Assert.Contains("date", past.Errors.Keys);

        var otherDept = await Assert.ThrowsAsync<ValidationAppException>(() => svc.SubmitAsync("shift",
            new SubmitRequest(Data(new { date = "2026-11-20", currentShift = "Sabah", requestedShift = "Akşam", swapWith = w.OtherEmployee.Id.ToString() })), w.EmployeeCtx));
        Assert.Contains("swapWith", otherDept.Errors.Keys);

        await svc.SubmitAsync("shift", new SubmitRequest(Data(new { date = "2026-11-20", currentShift = "Sabah", requestedShift = "Akşam" })), w.EmployeeCtx);
        var dup = await Assert.ThrowsAsync<AppException>(() => svc.SubmitAsync("shift",
            new SubmitRequest(Data(new { date = "2026-11-20", currentShift = "Akşam", requestedShift = "Sabah" })), w.EmployeeCtx));
        Assert.Contains("zaten", dup.Message);
    }

    [Fact]
    public async Task Shift_is_blocked_on_a_leave_day()
    {
        var (w, _) = await ShiftWorld();
        var leaveForm = new FormDefinition { Key = "leave", Name = "İzin", Module = "İK", Kind = FormKinds.Leave };
        w.Db.FormDefinitions.Add(leaveForm);
        w.Db.FormSubmissions.Add(new FormSubmission
        {
            Number = "FRM-1", FormDefinition = leaveForm, SubmitterId = w.Employee.Id, Status = SubmissionStatus.Approved,
            Data = """{"leaveType":"Yıllık İzin","startDate":"2026-11-19","endDate":"2026-11-21"}""",
        });
        await w.Db.SaveChangesAsync();
        var ex = await Assert.ThrowsAsync<AppException>(() => w.Workflow().SubmitAsync("shift",
            new SubmitRequest(Data(new { date = "2026-11-20", currentShift = "Sabah", requestedShift = "Akşam" })), w.EmployeeCtx));
        Assert.Contains("izin kaydınız", ex.Message);
    }
}
