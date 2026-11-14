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

// ============================================================ DTO'lar
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

public class SubmissionQuery : PagedQuery
{
    /// <summary>mine | approvals | assigned | history | all</summary>
    public string Scope { get; set; } = "mine";
    public string? FormKey { get; set; }
    public string? Module { get; set; }
    public SubmissionStatus? Status { get; set; }
}

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

public class SaveFormValidator : AbstractValidator<SaveFormRequest>
{
    public SaveFormValidator()
    {
        RuleFor(x => x.Key).NotEmpty().Matches("^[a-z0-9-]{3,60}$").WithMessage("Anahtar küçük harf, rakam ve tire içermelidir (ör. donanim-talebi).");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Form adı zorunludur.").MaximumLength(120);
        RuleFor(x => x.Module).NotEmpty().WithMessage("Modül (departman grubu) zorunludur.").MaximumLength(80);
        RuleFor(x => x.Kind).Must(k => FormKinds.All.Contains(k)).WithMessage("Geçersiz form türü.");
        RuleFor(x => x).Must(x => x.Steps.Where(st => st.ApproverType == ApproverType.FieldUser).All(st => x.Fields.Any(f => f.Key == st.FieldKey && f.Type == FieldTypes.User)))
            .WithName("steps").WithMessage("\"Formdaki kişi\" adımı, formdaki bir kişi (user) alanını göstermelidir.");
        RuleFor(x => x.Fields).NotEmpty().WithMessage("Form en az bir alan içermelidir.")
            .Must(f => f.Select(x => x.Key).Distinct().Count() == f.Count).WithMessage("Alan anahtarları benzersiz olmalıdır.");
        RuleForEach(x => x.Fields).ChildRules(f =>
        {
            f.RuleFor(x => x.Key).NotEmpty().Matches("^[a-zA-Z][a-zA-Z0-9_]{0,59}$").WithMessage("Alan anahtarı harfle başlamalı; harf, rakam ve _ içerebilir.");
            f.RuleFor(x => x.Label).NotEmpty().WithMessage("Alan etiketi zorunludur.").MaximumLength(300);
            f.RuleFor(x => x.Type).Must(t => FieldTypes.All.Contains(t)).WithMessage("Geçersiz alan tipi.");
            f.RuleFor(x => x.Options).NotEmpty().When(x => x.Type is FieldTypes.Select or FieldTypes.MultiSelect).WithMessage("Seçim alanları en az bir seçenek içermelidir.");
        });
        RuleForEach(x => x.Steps).ChildRules(s =>
        {
            s.RuleFor(x => x.Name).NotEmpty().WithMessage("Adım adı zorunludur.");
            s.RuleFor(x => x.RoleCode).NotEmpty().When(x => x.ApproverType == ApproverType.Role).WithMessage("Rol adımında rol seçilmelidir.");
            s.RuleFor(x => x.UserId).NotNull().When(x => x.ApproverType == ApproverType.User).WithMessage("Kişi adımında onaylayıcı seçilmelidir.");
            s.RuleFor(x => x.FieldKey).NotEmpty().When(x => x.ApproverType == ApproverType.FieldUser).WithMessage("Formdaki kişi adımında alan seçilmelidir.");
        });
    }
}

// ============================================================ İş akışı motoru
/// <summary>
/// Türden bağımsız iş akışı motoru: form tanımları, alan doğrulama, onay adımları, kararlar, bildirim olayları.
/// Türe özgü kurallar <see cref="IFormKindHandler"/> uygulamalarındadır (<see cref="FormKindRegistry"/>).
/// </summary>
public class WorkflowService(AppDbContext db, IEventBus events, FormKindRegistry kinds, TimeProvider clock, ILogger<WorkflowService> logger)
{
    private static JsonSerializerOptions Json => WorkflowData.Json;
    private static CultureInfo Tr => WorkflowData.Tr;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ---------------- Form tanımları ----------------
    public async Task<List<FormSummary>> FormsAsync(UserContext me, bool includeInactive, CancellationToken ct)
    {
        var forms = await db.FormDefinitions.AsNoTracking().Include(f => f.OwnerDepartment)
            .Where(f => includeInactive || f.IsActive).OrderBy(f => f.Module).ThenBy(f => f.SortOrder).ToListAsync(ct);
        var pending = await db.FormSubmissions.Where(s => s.Status == SubmissionStatus.Pending && s.PendingApproverIds.Contains(me.Id))
            .GroupBy(s => s.FormDefinitionId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return forms.Where(f => includeInactive || CanSubmit(f, me)).Select(f => Summary(f, pending.GetValueOrDefault(f.Id))).ToList();
    }

    /// <summary>Form tanımı. raw = true ise form tasarımcısı için alanlar olduğu gibi döner (türe özgü zenginleştirme — ör. mağaza listesi — kalıcılaşmasın).</summary>
    public async Task<FormDefinitionDto> FormAsync(string key, UserContext me, CancellationToken ct, bool raw = false)
    {
        var f = await db.FormDefinitions.AsNoTracking().Include(x => x.OwnerDepartment).FirstOrDefaultAsync(x => x.Key == key, ct) ?? throw AppException.NotFound("Form");
        if (!f.IsActive && !CanManage(f, me)) throw AppException.NotFound("Form");
        var handler = kinds.For(f.Kind);
        return new FormDefinitionDto(f.Id, f.Key, f.Name, f.Description, f.Module, f.Icon, f.Color, f.Kind, Dept(f.OwnerDepartment),
            raw && CanManage(f, me) ? f.Fields : await handler.PrepareFieldsAsync(f, ct), f.Steps, f.AllowedRoles, f.IsActive, CanManage(f, me), await handler.UserContextAsync(f, me.Id, ct));
    }

    public async Task<FormDefinitionDto> SaveFormAsync(int? id, SaveFormRequest req, UserContext me, CancellationToken ct)
    {
        if (await db.FormDefinitions.AnyAsync(f => f.Key == req.Key && f.Id != id, ct)) throw AppException.Conflict("Bu anahtarla bir form zaten var.");
        foreach (var s in req.Steps.Where(s => s.ApproverType == ApproverType.Role))
            if (!await db.Roles.AnyAsync(r => r.Code == s.RoleCode, ct)) throw AppException.BadRequest($"'{s.RoleCode}' rolü bulunamadı.");
        FormDefinition f;
        if (id is { } e)
        {
            f = await db.FormDefinitions.FirstOrDefaultAsync(x => x.Id == e, ct) ?? throw AppException.NotFound("Form");
            if (!CanManage(f, me)) throw AppException.Forbidden("Bu formu yalnızca sahibi departmanın yöneticisi veya sistem yöneticisi düzenleyebilir.");
            if (f.Kind != req.Kind && await db.FormSubmissions.AnyAsync(s => s.FormDefinitionId == e, ct))
                throw AppException.BadRequest("Başvurusu olan formun türü değiştirilemez.");
        }
        else
        {
            if (!me.IsAdmin && req.OwnerDepartmentId != me.DepartmentId) throw AppException.Forbidden("Yalnızca kendi departmanınız adına form oluşturabilirsiniz.");
            db.FormDefinitions.Add(f = new FormDefinition { Key = req.Key, Name = req.Name, Module = req.Module, SortOrder = 1000 });
        }
        f.Key = req.Key; f.Name = req.Name.Trim(); f.Description = req.Description?.Trim(); f.Module = req.Module.Trim(); f.Icon = req.Icon; f.Color = req.Color;
        f.Kind = req.Kind; f.OwnerDepartmentId = req.OwnerDepartmentId; f.Fields = req.Fields; f.Steps = req.Steps; f.AllowedRoles = req.AllowedRoles ?? []; f.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Form tanımı kaydedildi {FormKey} (işlemi yapan {ActorId})", f.Key, me.Id);
        return await FormAsync(f.Key, me, ct, raw: true);
    }

    // ---------------- Başvuru ----------------
    public async Task<SubmissionDetail> SubmitAsync(string key, SubmitRequest req, UserContext me, CancellationToken ct = default)
    {
        var form = await db.FormDefinitions.Include(f => f.OwnerDepartment).FirstOrDefaultAsync(f => f.Key == key && f.IsActive, ct) ?? throw AppException.NotFound("Form");
        if (!CanSubmit(form, me)) throw AppException.Forbidden("Bu formu kullanma yetkiniz bulunmamaktadır.");
        var submitter = await db.Users.Include(u => u.Department).FirstAsync(u => u.Id == me.Id, ct);

        var (data, fileRefs, errors) = await ValidateDataAsync(form, req.Data, me, ct);
        if (errors.Count > 0)
            throw new ValidationAppException(errors);

        var (summary, amount) = BuildSummary(form, data);
        // Türe özgü kurallar (izin, limit, ciro, vardiya …) ilgili işleyicide
        summary = await kinds.For(form.Kind).ValidateAsync(new FormSubmitContext(form, data, amount, me), ct) ?? summary;

        var s = new FormSubmission
        {
            Number = "TMP-" + Guid.NewGuid().ToString("N")[..20], FormDefinitionId = form.Id, SubmitterId = me.Id, DepartmentId = submitter.DepartmentId,
            Data = JsonSerializer.Serialize(data, Json), Summary = summary, Amount = amount, Status = SubmissionStatus.Pending, CreatedAt = Now,
        };
        s.Actions.Add(new SubmissionAction { Action = SubmissionActionType.Submitted, ActorId = me.Id, Comment = "Başvuru oluşturuldu.", CreatedAt = Now });
        foreach (var (field, fileId) in fileRefs) s.Files.Add(new SubmissionFile { FileId = fileId, FieldKey = field });
        db.FormSubmissions.Add(s);
        await db.SaveChangesAsync(ct);
        s.Number = $"FRM-{Now.Year}-{s.Id:000000}";

        await AdvanceAsync(s, form, submitter, 0, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Başvuru oluşturuldu {Number} ({FormKey})", s.Number, form.Key);
        return await DetailAsync(s.Id, me, ct);
    }

    /// <summary>
    /// Akışı belirtilen adımdan itibaren ilerletir: onaylayıcısı başvuru sahibi olan veya
    /// bulunamayan adımlar atlanır, tüm adımlar bitince başvuru onaylanır.
    /// </summary>
    private async Task AdvanceAsync(FormSubmission s, FormDefinition form, User submitter, int fromStep, CancellationToken ct)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(s.Data, Json) ?? [];
        for (var i = fromStep; i < form.Steps.Count; i++)
        {
            var step = form.Steps[i];
            var approvers = (await ResolveApproversAsync(step, form, submitter, data, ct)).Where(id => id != submitter.Id).Distinct().ToList();
            if (approvers.Count == 0)
            {
                s.Actions.Add(new SubmissionAction { Action = SubmissionActionType.Skipped, StepIndex = i, StepName = step.Name, Comment = "Onaylayıcı başvuru sahibi olduğu veya tanımlı olmadığı için adım otomatik geçildi.", CreatedAt = Now });
                continue;
            }
            s.CurrentStep = i;
            s.PendingApproverIds = approvers;
            s.Status = SubmissionStatus.Pending;
            events.Enqueue(new WorkflowEvent("step_pending", s.Id, s.Number, form.Name, s.SubmitterId, approvers, step.Name) { ActorId = submitter.Id, ActorName = submitter.FullName });
            return;
        }
        await FinalizeApprovalAsync(s, form, ct);
    }

    private async Task FinalizeApprovalAsync(FormSubmission s, FormDefinition form, CancellationToken ct)
    {
        s.Status = SubmissionStatus.Approved;
        s.CurrentStep = form.Steps.Count;
        s.PendingApproverIds = [];
        s.AssigneeId = form.OwnerDepartment?.ManagerId ?? (form.OwnerDepartmentId is { } od ? await db.Departments.Where(d => d.Id == od).Select(d => d.ManagerId).FirstOrDefaultAsync(ct) : null);

        await kinds.For(form.Kind).OnApprovedAsync(s, WorkflowData.Parse(s.Data), ct);
        var recipients = new List<Guid> { s.SubmitterId };
        if (s.AssigneeId is { } a) recipients.Add(a);
        events.Enqueue(new WorkflowEvent("approved", s.Id, s.Number, form.Name, s.SubmitterId, recipients));
    }

    private async Task<List<Guid>> ResolveApproversAsync(WorkflowStep step, FormDefinition form, User submitter, Dictionary<string, JsonElement> data, CancellationToken ct)
    {
        async Task<List<Guid>> Admins() => await db.Users.Where(u => u.IsActive && u.Roles.Any(r => r.Role.Code == RoleCodes.Admin)).Select(u => u.Id).ToListAsync(ct);
        Guid? deptManager = submitter.Department?.ManagerId;
        switch (step.ApproverType)
        {
            case ApproverType.Manager:
                var m = submitter.ManagerId ?? deptManager;
                return m is { } mid ? [mid] : await Admins();
            case ApproverType.DepartmentManager:
                return deptManager is { } dm && dm != submitter.Id ? [dm] : submitter.ManagerId is { } sm ? [sm] : await Admins();
            case ApproverType.Role:
                var ids = await db.Users.Where(u => u.IsActive && u.Roles.Any(r => r.Role.Code == step.RoleCode)).Select(u => u.Id).ToListAsync(ct);
                return ids.Count > 0 ? ids : await Admins();
            case ApproverType.User:
                return step.UserId is { } uid && await db.Users.AnyAsync(u => u.Id == uid && u.IsActive, ct) ? [uid] : await Admins();
            case ApproverType.FieldUser:
                // Formda seçilen kişi; alan boşsa veya kişi pasifse adım atlanır (yöneticilere düşmez).
                if (step.FieldKey is { } fk && data.TryGetValue(fk, out var fv) && fv.ValueKind == JsonValueKind.Object
                    && fv.TryGetProperty("id", out var idEl) && Guid.TryParse(idEl.GetString(), out var fieldUser)
                    && await db.Users.AnyAsync(u => u.Id == fieldUser && u.IsActive, ct))
                    return [fieldUser];
                return [];
            case ApproverType.ResponsibleDepartment:
                var owner = form.OwnerDepartmentId is { } od ? await db.Departments.Where(d => d.Id == od).Select(d => d.ManagerId).FirstOrDefaultAsync(ct) : null;
                return owner is { } o ? [o] : await Admins();
            default:
                return await Admins();
        }
    }

    public async Task<SubmissionDetail> DecideAsync(int id, bool approve, DecisionRequest req, UserContext me, CancellationToken ct = default)
    {
        var s = await db.FormSubmissions.Include(x => x.FormDefinition).ThenInclude(f => f.OwnerDepartment).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Başvuru");
        if (s.Status != SubmissionStatus.Pending) throw AppException.BadRequest("Bu başvuru onay aşamasında değil.");
        if (!s.PendingApproverIds.Contains(me.Id)) throw AppException.Forbidden("Bu adımda onay yetkiniz bulunmamaktadır.");
        if (!approve && string.IsNullOrWhiteSpace(req.Comment)) throw AppException.BadRequest("Ret gerekçesi yazmanız gerekmektedir.");
        var step = s.FormDefinition.Steps[s.CurrentStep];

        s.Actions.Add(new SubmissionAction { Action = approve ? SubmissionActionType.Approved : SubmissionActionType.Rejected, ActorId = me.Id, StepIndex = s.CurrentStep, StepName = step.Name, Comment = req.Comment?.Trim(), CreatedAt = Now });
        if (approve)
        {
            var submitter = await db.Users.Include(u => u.Department).FirstAsync(u => u.Id == s.SubmitterId, ct);
            await AdvanceAsync(s, s.FormDefinition, submitter, s.CurrentStep + 1, ct);
        }
        else
        {
            s.Status = SubmissionStatus.Rejected;
            s.PendingApproverIds = [];
            events.Enqueue(new WorkflowEvent("rejected", s.Id, s.Number, s.FormDefinition.Name, s.SubmitterId, [s.SubmitterId], step.Name, req.Comment) { ActorId = me.Id, ActorName = me.Name });
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Başvuru {Number} {Decision} (adım {Step}, karar veren {ActorId})", s.Number, approve ? "onaylandı" : "reddedildi", step.Name, me.Id);
        return await DetailAsync(id, me, ct);
    }

    public async Task<SubmissionDetail> CompleteAsync(int id, DecisionRequest req, UserContext me, CancellationToken ct)
    {
        var s = await db.FormSubmissions.Include(x => x.FormDefinition).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Başvuru");
        if (s.Status != SubmissionStatus.Approved) throw AppException.BadRequest("Yalnızca onaylanmış başvurular tamamlanabilir.");
        if (!await CanCompleteAsync(s, me, ct)) throw AppException.Forbidden("Bu başvuruyu tamamlama yetkiniz bulunmamaktadır.");
        s.Status = SubmissionStatus.Completed;
        s.CompletedAt = Now;
        s.Actions.Add(new SubmissionAction { Action = SubmissionActionType.Completed, ActorId = me.Id, Comment = req.Comment?.Trim() ?? "İşlem tamamlandı.", CreatedAt = Now });
        events.Enqueue(new WorkflowEvent("completed", s.Id, s.Number, s.FormDefinition.Name, s.SubmitterId, [s.SubmitterId], null, req.Comment) { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        return await DetailAsync(id, me, ct);
    }

    public async Task<SubmissionDetail> CancelAsync(int id, UserContext me, CancellationToken ct)
    {
        var s = await db.FormSubmissions.Include(x => x.FormDefinition).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Başvuru");
        if (s.SubmitterId != me.Id) throw AppException.Forbidden("Yalnızca kendi başvurunuzu iptal edebilirsiniz.");
        if (s.Status != SubmissionStatus.Pending) throw AppException.BadRequest("Yalnızca onay bekleyen başvurular iptal edilebilir.");
        var approvers = s.PendingApproverIds.ToList();
        s.Status = SubmissionStatus.Cancelled;
        s.PendingApproverIds = [];
        s.Actions.Add(new SubmissionAction { Action = SubmissionActionType.Cancelled, ActorId = me.Id, Comment = "Başvuru sahibi tarafından iptal edildi.", CreatedAt = Now });
        events.Enqueue(new WorkflowEvent("cancelled", s.Id, s.Number, s.FormDefinition.Name, s.SubmitterId, approvers) { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        return await DetailAsync(id, me, ct);
    }

    public async Task<SubmissionDetail> CommentAsync(int id, DecisionRequest req, UserContext me, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Comment)) throw AppException.BadRequest("Açıklama boş olamaz.");
        var s = await db.FormSubmissions.Include(x => x.FormDefinition).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Başvuru");
        if (!await CanViewAsync(s, me, ct)) throw AppException.Forbidden();
        s.Actions.Add(new SubmissionAction { Action = SubmissionActionType.Commented, ActorId = me.Id, Comment = req.Comment.Trim(), CreatedAt = Now });
        var recipients = s.PendingApproverIds.Append(s.SubmitterId).Where(x => x != me.Id).Distinct().ToList();
        events.Enqueue(new WorkflowEvent("commented", s.Id, s.Number, s.FormDefinition.Name, s.SubmitterId, recipients, null, req.Comment) { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        return await DetailAsync(id, me, ct);
    }

    // ---------------- Listeleme ve detay ----------------
    public async Task<PagedResult<SubmissionListItem>> ListAsync(SubmissionQuery q, UserContext me, CancellationToken ct)
    {
        var query = db.FormSubmissions.AsNoTracking().AsQueryable();
        var id = me.Id;
        query = q.Scope switch
        {
            "approvals" => query.Where(s => s.Status == SubmissionStatus.Pending && s.PendingApproverIds.Contains(id)),
            "assigned" => query.Where(s => s.AssigneeId == id && s.Status == SubmissionStatus.Approved),
            "history" => query.Where(s => s.Actions.Any(a => a.ActorId == id && (a.Action == SubmissionActionType.Approved || a.Action == SubmissionActionType.Rejected || a.Action == SubmissionActionType.Completed))),
            "all" when me.Has(Permissions.SubmissionsViewAll) => query,
            "all" => query.Where(s => s.FormDefinition.OwnerDepartment != null && s.FormDefinition.OwnerDepartment.ManagerId == id || s.AssigneeId == id || s.SubmitterId == id),
            _ => query.Where(s => s.SubmitterId == id),
        };
        if (!string.IsNullOrWhiteSpace(q.FormKey)) query = query.Where(s => s.FormDefinition.Key == q.FormKey);
        if (!string.IsNullOrWhiteSpace(q.Module)) query = query.Where(s => s.FormDefinition.Module == q.Module);
        if (q.Status is { } st) query = query.Where(s => s.Status == st);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var t = q.Search.Like();
            query = query.Where(s => EF.Functions.ILike(s.Number, t) || (s.Summary != null && EF.Functions.ILike(s.Summary, t))
                                     || EF.Functions.ILike(s.Submitter.FirstName + " " + s.Submitter.LastName, t) || EF.Functions.ILike(s.FormDefinition.Name, t));
        }
        var page = await query.OrderByDescending(s => s.UpdatedAt).ToPagedAsync(q, s => new
        {
            s.Id, s.Number, s.FormDefinition.Key, s.FormDefinition.Name, s.FormDefinition.Module, s.FormDefinition.Icon, s.FormDefinition.Color, s.FormDefinition.Steps,
            Submitter = new UserRef(s.Submitter.Id, s.Submitter.FirstName + " " + s.Submitter.LastName, s.Submitter.Email),
            Dept = s.Department == null ? null : s.Department.Name, s.Status, s.Summary, s.Amount, s.CurrentStep, s.CreatedAt, s.UpdatedAt,
        }, ct);
        return new PagedResult<SubmissionListItem>(page.Items.Select(s => new SubmissionListItem(s.Id, s.Number, s.Key, s.Name, s.Module, s.Icon, s.Color, s.Submitter, s.Dept,
            s.Status, s.Summary, s.Amount, s.Status == SubmissionStatus.Pending && s.CurrentStep < s.Steps.Count ? s.Steps[s.CurrentStep].Name : null,
            s.CurrentStep, s.Steps.Count, s.CreatedAt, s.UpdatedAt)).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<SubmissionDetail> DetailAsync(int id, UserContext me, CancellationToken ct = default)
    {
        var s = await db.FormSubmissions.AsNoTracking().Include(x => x.FormDefinition).ThenInclude(f => f.OwnerDepartment)
            .Include(x => x.Submitter).ThenInclude(u => u.JobTitle).Include(x => x.Department).Include(x => x.Assignee)
            .Include(x => x.Actions).ThenInclude(a => a.Actor).Include(x => x.Files).ThenInclude(f => f.File)
            .AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Başvuru");
        if (!await CanViewAsync(s, me, ct)) throw AppException.Forbidden("Bu başvuruyu görüntüleme yetkiniz bulunmamaktadır.");

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(s.Data, Json) ?? [];
        var fields = s.FormDefinition.Fields.Where(f => f.Type != FieldTypes.Description)
            .Select(f => new SubmissionField(f.Key, f.Label, f.Type, data.TryGetValue(f.Key, out var v) ? v : null, Display(f, data.GetValueOrDefault(f.Key)))).ToList();
        fields.AddRange(kinds.For(s.FormDefinition.Kind).ExtraFields(data));

        var approverIds = s.PendingApproverIds;
        var approverUsers = await db.Users.AsNoTracking().Where(u => approverIds.Contains(u.Id)).Select(u => new UserRef(u.Id, u.FirstName + " " + u.LastName, u.Email)).ToListAsync(ct);
        var steps = s.FormDefinition.Steps.Select((st, i) =>
        {
            var last = s.Actions.Where(a => a.StepIndex == i).OrderByDescending(a => a.CreatedAt).FirstOrDefault();
            var state = last?.Action switch
            {
                SubmissionActionType.Approved => "approved",
                SubmissionActionType.Rejected => "rejected",
                SubmissionActionType.Skipped => "skipped",
                _ => s.Status == SubmissionStatus.Pending && i == s.CurrentStep ? "current" : s.Status is SubmissionStatus.Rejected or SubmissionStatus.Cancelled ? "cancelled" : "waiting",
            };
            var who = state == "current" ? approverUsers : last?.Actor is { } a ? [new UserRef(a.Id, a.FullName, a.Email)] : [];
            return new SubmissionStepDto(i, st.Name, state, who);
        }).ToList();

        var pendingCount = 0;
        return new SubmissionDetail(s.Id, s.Number, Summary(s.FormDefinition, pendingCount), new UserRef(s.Submitter.Id, s.Submitter.FullName, s.Submitter.Email),
            s.Submitter.JobTitle?.Name, Dept(s.Department), s.Status, s.Summary, s.Amount, fields, steps,
            s.Actions.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).Select(a => new SubmissionActionDto(a.Id, a.Action, a.StepName,
                a.Actor is null ? null : new UserRef(a.Actor.Id, a.Actor.FullName, a.Actor.Email), a.Comment, a.CreatedAt)).ToList(),
            s.Files.Select(f => new FileDto(f.File.Id, f.File.FileName, f.File.ContentType, f.File.Size, f.File.CreatedAt)).ToList(),
            s.Assignee is null ? null : new UserRef(s.Assignee.Id, s.Assignee.FullName, s.Assignee.Email),
            s.Status == SubmissionStatus.Pending && s.PendingApproverIds.Contains(me.Id),
            s.Status == SubmissionStatus.Approved && await CanCompleteAsync(s, me, ct),
            s.Status == SubmissionStatus.Pending && s.SubmitterId == me.Id,
            s.CreatedAt, s.UpdatedAt, s.CompletedAt);
    }

    // ---------------- yardımcılar ----------------
    private static bool CanSubmit(FormDefinition f, UserContext me) => f.AllowedRoles.Count == 0 || f.AllowedRoles.Any(me.HasRole) || me.IsAdmin;
    private static bool CanManage(FormDefinition f, UserContext me) => me.IsAdmin || (me.Has(Permissions.FormsManage) && f.OwnerDepartmentId == me.DepartmentId);
    private static DeptRef? Dept(Department? d) => d is null ? null : new DeptRef(d.Id, d.Name);
    private static FormSummary Summary(FormDefinition f, int pending) =>
        new(f.Id, f.Key, f.Name, f.Description, f.Module, f.Icon, f.Color, f.Kind, Dept(f.OwnerDepartment), f.Steps.Count, f.IsActive, pending);

    private async Task<bool> CanViewAsync(FormSubmission s, UserContext me, CancellationToken ct)
    {
        if (s.SubmitterId == me.Id || s.AssigneeId == me.Id || s.PendingApproverIds.Contains(me.Id) || me.Has(Permissions.SubmissionsViewAll) || me.IsAdmin) return true;
        if (await db.SubmissionActions.AnyAsync(a => a.SubmissionId == s.Id && a.ActorId == me.Id, ct)) return true;
        var ownerDept = s.FormDefinition.OwnerDepartmentId;
        return ownerDept is not null && await db.Departments.AnyAsync(d => d.Id == ownerDept && d.ManagerId == me.Id, ct);
    }

    private async Task<bool> CanCompleteAsync(FormSubmission s, UserContext me, CancellationToken ct)
    {
        if (s.AssigneeId == me.Id || me.IsAdmin) return true;
        var ownerDept = s.FormDefinition.OwnerDepartmentId;
        return ownerDept is not null && (me.DepartmentId == ownerDept || await db.Departments.AnyAsync(d => d.Id == ownerDept && d.ManagerId == me.Id, ct));
    }

    /// <summary>Gönderilen veriyi form tanımına göre sunucu tarafında doğrular ve normalleştirir.</summary>
    private async Task<(Dictionary<string, object?> Data, List<(string Field, Guid FileId)> Files, Dictionary<string, string[]> Errors)> ValidateDataAsync(
        FormDefinition form, Dictionary<string, JsonElement> input, UserContext me, CancellationToken ct)
    {
        var data = new Dictionary<string, object?>();
        var files = new List<(string, Guid)>();
        var errors = new Dictionary<string, string[]>();
        void Err(string key, string msg) => errors[key] = [msg];

        foreach (var f in form.Fields.Where(f => f.Type != FieldTypes.Description))
        {
            input.TryGetValue(f.Key, out var v);
            var empty = v.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                        || (v.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(v.GetString()))
                        || (v.ValueKind == JsonValueKind.Array && v.GetArrayLength() == 0)
                        || (f.Type == FieldTypes.Checkbox && v.ValueKind == JsonValueKind.False && f.Required);
            if (empty)
            {
                if (f.Required) Err(f.Key, $"{f.Label} zorunludur.");
                continue;
            }
            try
            {
                switch (f.Type)
                {
                    case FieldTypes.Number or FieldTypes.Currency:
                        var n = v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : decimal.Parse(v.GetString()!.Replace(',', '.'), CultureInfo.InvariantCulture);
                        if (f.Min is { } min && n < min) { Err(f.Key, $"{f.Label} en az {min} olmalıdır."); continue; }
                        if (f.Max is { } max && n > max) { Err(f.Key, $"{f.Label} en fazla {max} olabilir."); continue; }
                        data[f.Key] = n;
                        break;
                    case FieldTypes.Date:
                        if (!DateOnly.TryParse(v.GetString(), CultureInfo.InvariantCulture, out var d)) { Err(f.Key, $"{f.Label} geçerli bir tarih olmalıdır."); continue; }
                        data[f.Key] = d.ToString("yyyy-MM-dd");
                        break;
                    case FieldTypes.Select:
                        var opt = v.GetString();
                        if (f.Options?.Contains(opt!) != true) { Err(f.Key, $"{f.Label} için geçersiz seçim."); continue; }
                        data[f.Key] = opt;
                        break;
                    case FieldTypes.MultiSelect:
                        var items = v.EnumerateArray().Select(x => x.GetString()!).Distinct().ToList();
                        if (items.Any(i => f.Options?.Contains(i) != true)) { Err(f.Key, $"{f.Label} için geçersiz seçim."); continue; }
                        data[f.Key] = items;
                        break;
                    case FieldTypes.Checkbox:
                        data[f.Key] = v.ValueKind == JsonValueKind.True;
                        break;
                    case FieldTypes.User:
                        if (!Guid.TryParse(v.ValueKind == JsonValueKind.Object ? v.GetProperty("id").GetString() : v.GetString(), out var uid)) { Err(f.Key, $"{f.Label} geçersiz."); continue; }
                        var picked = await db.Users.AsNoTracking().Where(u => u.Id == uid && u.IsActive).Select(u => new FieldUserValue(u.Id, u.FirstName + " " + u.LastName)).FirstOrDefaultAsync(ct);
                        if (picked is null) { Err(f.Key, $"{f.Label}: seçilen çalışan bulunamadı veya aktif değil."); continue; }
                        data[f.Key] = picked;
                        break;
                    case FieldTypes.Email:
                        var email = v.GetString()!.Trim();
                        if (!email.Contains('@') || email.Length > 200) { Err(f.Key, $"{f.Label} geçerli bir e-posta olmalıdır."); continue; }
                        data[f.Key] = email;
                        break;
                    case FieldTypes.File:
                        if (!Guid.TryParse(v.ValueKind == JsonValueKind.Object ? v.GetProperty("id").GetString() : v.GetString(), out var fid)) { Err(f.Key, $"{f.Label} geçersiz."); continue; }
                        var file = await db.Files.AsNoTracking().FirstOrDefaultAsync(x => x.Id == fid && x.UploadedById == me.Id, ct);
                        if (file is null) { Err(f.Key, $"{f.Label} bulunamadı. Dosyayı yeniden yükleyiniz."); continue; }
                        data[f.Key] = new { id = file.Id, name = file.FileName, size = file.Size };
                        files.Add((f.Key, file.Id));
                        break;
                    default:
                        var text = v.ValueKind == JsonValueKind.String ? v.GetString()!.Trim() : v.ToString();
                        var maxLen = f.MaxLength ?? (f.Type == FieldTypes.Textarea ? 4000 : 200);
                        if (text.Length > maxLen) { Err(f.Key, $"{f.Label} en fazla {maxLen} karakter olabilir."); continue; }
                        data[f.Key] = text;
                        break;
                }
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or KeyNotFoundException)
            {
                Err(f.Key, $"{f.Label} için geçersiz değer.");
            }
        }
        return (data, files, errors);
    }

    private static (string? Summary, decimal? Amount) BuildSummary(FormDefinition form, Dictionary<string, object?> data)
    {
        decimal? amount = data.TryGetValue("amount", out var a) && a is decimal d ? d
            : form.Fields.FirstOrDefault(f => f.Type == FieldTypes.Currency && data.ContainsKey(f.Key)) is { } cf ? (decimal?)data[cf.Key] : null;
        var parts = form.Fields.Where(f => f.Type is FieldTypes.Text or FieldTypes.Select && data.ContainsKey(f.Key)).Take(2)
            .Select(f => data[f.Key]?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (amount is { } m) parts.Add(m.ToString("C2", Tr));
        var summary = string.Join(" · ", parts);
        return (summary.Length > 300 ? summary[..300] : summary, amount);
    }

    private static string Display(FormField f, JsonElement v)
    {
        if (v.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return "—";
        return f.Type switch
        {
            FieldTypes.Currency => v.ValueKind == JsonValueKind.Number ? v.GetDecimal().ToString("C2", Tr) : v.ToString(),
            FieldTypes.Date => DateOnly.TryParse(v.GetString(), out var d) ? d.ToString("dd MMMM yyyy", Tr) : v.ToString(),
            FieldTypes.MultiSelect => string.Join(", ", v.EnumerateArray().Select(x => x.GetString())),
            FieldTypes.Checkbox => v.ValueKind == JsonValueKind.True ? "Evet" : "Hayır",
            FieldTypes.File => v.ValueKind == JsonValueKind.Object && v.TryGetProperty("name", out var n) ? n.GetString() ?? "Dosya" : "Dosya",
            FieldTypes.User => v.ValueKind == JsonValueKind.Object && v.TryGetProperty("name", out var un) ? un.GetString() ?? "—" : "—",
            _ => v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString(),
        };
    }
}

/// <summary>Alan bazlı doğrulama hataları (400) — standart yanıt zarfının "errors" alanına yazılır.</summary>
public class ValidationAppException(Dictionary<string, string[]> errors) : AppException(System.Net.HttpStatusCode.BadRequest, "Gönderilen bilgilerde hata var. Lütfen alanları kontrol ediniz.", "VALIDATION")
{
    public Dictionary<string, string[]> Errors { get; } = errors;
}

// ============================================================ Controller'lar
[Route("api/forms")]
public class FormsController(WorkflowService workflow) : ApiController
{
    /// <summary>Kullanıcının başvurabileceği departman uygulamaları.</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool all = false, CancellationToken ct = default) =>
        Ok(await workflow.FormsAsync(Me, all && (Me.Has(Permissions.FormsManage) || Me.IsAdmin), ct));

    [HttpGet("{key}")]
    // edit=true: form tasarımcısı için ham tanım (yalnızca formu yönetebilenler)
    public async Task<IActionResult> Get(string key, [FromQuery] bool edit = false, CancellationToken ct = default) => Ok(await workflow.FormAsync(key, Me, ct, edit));

    [HasPermission(Permissions.FormsManage), HttpPost]
    public async Task<IActionResult> Create(SaveFormRequest req, CancellationToken ct) => Created(await workflow.SaveFormAsync(null, req, Me, ct), "Form oluşturuldu.");

    [HasPermission(Permissions.FormsManage), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SaveFormRequest req, CancellationToken ct) => Ok(await workflow.SaveFormAsync(id, req, Me, ct), "Form güncellendi.");

    /// <summary>Form başvurusu oluşturur; ilk onay adımı otomatik yönlendirilir.</summary>
    [HttpPost("{key}/submissions")]
    public async Task<IActionResult> Submit(string key, SubmitRequest req, CancellationToken ct)
    {
        var s = await workflow.SubmitAsync(key, req, Me, ct);
        return Created(s, $"{s.Number} numaralı başvurunuz oluşturuldu.");
    }
}

[Route("api/submissions")]
public class SubmissionsController(WorkflowService workflow) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] SubmissionQuery q, CancellationToken ct) => Ok(await workflow.ListAsync(q, Me, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await workflow.DetailAsync(id, Me, ct));

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, DecisionRequest req, CancellationToken ct) => Ok(await workflow.DecideAsync(id, true, req, Me, ct), "Onaylandı.");

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, DecisionRequest req, CancellationToken ct) => Ok(await workflow.DecideAsync(id, false, req, Me, ct), "Reddedildi.");

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, DecisionRequest req, CancellationToken ct) => Ok(await workflow.CompleteAsync(id, req, Me, ct), "Başvuru tamamlandı.");

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) => Ok(await workflow.CancelAsync(id, Me, ct), "Başvuru iptal edildi.");

    [HttpPost("{id:int}/comments")]
    public async Task<IActionResult> Comment(int id, DecisionRequest req, CancellationToken ct) => Ok(await workflow.CommentAsync(id, req, Me, ct), "Açıklama eklendi.");
}

[Route("api/leave")]
public class LeaveController(LeaveService leave) : ApiController
{
    [HttpGet("balance")]
    public async Task<IActionResult> MyBalance(CancellationToken ct) => Ok(await leave.MyLeaveAsync(Me.Id, ct));

    [HasPermission(Permissions.LeaveManage), HttpGet("balances")]
    public async Task<IActionResult> Balances([FromQuery] string? search, CancellationToken ct) => Ok(await leave.LeaveBalancesAsync(null, search, ct));

    [HasPermission(Permissions.LeaveManage), HttpPut("balances/{userId:guid}")]
    public async Task<IActionResult> UpdateBalance(Guid userId, UpdateLeaveBalanceRequest req, CancellationToken ct) => Ok(await leave.UpdateLeaveAsync(userId, req, ct), "İzin bakiyesi güncellendi.");

    /// <summary>Kullanıcının departmanındaki izin takvimi.</summary>
    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        return Ok(await leave.LeaveCalendarAsync(Me, start, to ?? start.AddDays(60), ct));
    }
}

[Route("api")]
public class DepartmentAppsController(RevenueService revenue, ShiftService shifts) : ApiController
{
    /// <summary>Mağaza ciro özeti (son N gün). Yetkiye göre şirket geneli veya yalnızca kendi bildirimleri.</summary>
    [HttpGet("revenue/summary")]
    public async Task<IActionResult> Revenue([FromQuery] int days = 30, CancellationToken ct = default) => Ok(await revenue.SummaryAsync(Me, days, ct));

    /// <summary>Vardiya değişiklikleri (varsayılan: bugünden itibaren 30 gün).</summary>
    [HttpGet("shifts")]
    public async Task<IActionResult> Shifts([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(await shifts.ChangesAsync(Me, start, to ?? start.AddDays(30), ct));
    }
}

[Route("api/limits")]
public class LimitsController(SpendingLimitService limits) : ApiController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await limits.LimitsAsync(ct));

    [HasPermission(Permissions.LimitsManage), HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateLimitRequest req, CancellationToken ct) => Ok(await limits.UpdateLimitAsync(id, req, ct), "Limit güncellendi.");
}
