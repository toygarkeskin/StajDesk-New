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

public class CollaborationService(AppDbContext db, IEventBus events, ICacheService cache, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static UserRef R(User u) => new(u.Id, u.FullName, u.Email);

    // ---------------- Duyurular ve haberler ----------------
    public Task<List<AnnouncementDto>> AnnouncementsAsync(UserContext me, AnnouncementKind? kind, bool manage, int take, CancellationToken ct)
    {
        var now = Now;
        var q = db.Announcements.AsNoTracking().AsQueryable();
        if (!manage) q = q.Where(a => a.PublishAt <= now && (a.ExpiresAt == null || a.ExpiresAt > now) && (a.DepartmentId == null || a.DepartmentId == me.DepartmentId));
        if (kind is { } k) q = q.Where(a => a.Kind == k);
        return q.OrderByDescending(a => a.IsPinned).ThenByDescending(a => a.PublishAt).Take(Math.Clamp(take, 1, 100))
            .Select(a => new AnnouncementDto(a.Id, a.Title, a.Summary, a.Body, a.Kind, a.IsPublic, a.IsPinned, a.PublishAt, a.ExpiresAt,
                a.Department == null ? null : new DeptRef(a.Department.Id, a.Department.Name), new UserRef(a.Author.Id, a.Author.FirstName + " " + a.Author.LastName, a.Author.Email)))
            .ToListAsync(ct);
    }

    public async Task<AnnouncementDto> SaveAnnouncementAsync(int? id, SaveAnnouncementRequest req, UserContext me, CancellationToken ct)
    {
        Announcement a;
        if (id is { } e) a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == e, ct) ?? throw AppException.NotFound("Duyuru");
        else db.Announcements.Add(a = new Announcement { Title = req.Title, Summary = req.Summary, Body = req.Body, AuthorId = me.Id });
        a.Title = req.Title.Trim(); a.Summary = req.Summary.Trim(); a.Body = req.Body.Trim(); a.Kind = req.Kind; a.IsPublic = req.IsPublic; a.IsPinned = req.IsPinned;
        a.PublishAt = req.PublishAt?.ToUniversalTime() ?? (id is null ? Now : a.PublishAt); a.ExpiresAt = req.ExpiresAt?.ToUniversalTime(); a.DepartmentId = req.DepartmentId;
        if (id is null && req.Notify)
        {
            var recipients = await db.Users.Where(u => u.IsActive && u.Id != me.Id && (req.DepartmentId == null || u.DepartmentId == req.DepartmentId)).Select(u => u.Id).ToListAsync(ct);
            events.Enqueue(new DirectNotificationEvent(recipients, req.Kind == AnnouncementKind.News ? "Yeni haber" : "Yeni duyuru", a.Title, "info", "/announcements") { ActorId = me.Id, ActorName = me.Name });
        }
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.PublicHome);
        return (await AnnouncementsAsync(me, null, true, 100, ct)).First(x => x.Id == a.Id);
    }

    public async Task DeleteAnnouncementAsync(int id, CancellationToken ct)
    {
        await db.Announcements.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
        await cache.RemoveAsync(CacheKeys.PublicHome);
    }

    // ---------------- Banner ----------------
    public async Task<List<BannerDto>> BannersAsync(CancellationToken ct)
    {
        var now = Now;
        var list = await db.Banners.AsNoTracking().OrderBy(b => b.SortOrder).ToListAsync(ct);
        return list.Select(b => new BannerDto(b.Id, b.Title, b.Subtitle, b.ImageUrl, b.Theme, b.LinkUrl, b.LinkText, b.SortOrder, b.StartsAt, b.EndsAt, b.IsActive, b.IsLive(now))).ToList();
    }

    public async Task<BannerDto> SaveBannerAsync(int? id, SaveBannerRequest req, CancellationToken ct)
    {
        Banner b;
        if (id is { } e) b = await db.Banners.FirstOrDefaultAsync(x => x.Id == e, ct) ?? throw AppException.NotFound("Banner");
        else db.Banners.Add(b = new Banner { Title = req.Title, SortOrder = (await db.Banners.MaxAsync(x => (int?)x.SortOrder, ct) ?? 0) + 1 });
        b.Title = req.Title.Trim(); b.Subtitle = req.Subtitle?.Trim(); b.ImageUrl = req.ImageUrl?.Trim(); b.Theme = req.Theme; b.LinkUrl = req.LinkUrl?.Trim(); b.LinkText = req.LinkText?.Trim();
        if (id is not null) b.SortOrder = req.SortOrder;
        b.StartsAt = req.StartsAt?.ToUniversalTime(); b.EndsAt = req.EndsAt?.ToUniversalTime(); b.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.PublicHome);
        return (await BannersAsync(ct)).First(x => x.Id == b.Id);
    }

    public async Task ReorderBannersAsync(ReorderRequest req, CancellationToken ct)
    {
        var banners = await db.Banners.Where(b => req.Ids.Contains(b.Id)).ToListAsync(ct);
        for (var i = 0; i < req.Ids.Count; i++) if (banners.FirstOrDefault(b => b.Id == req.Ids[i]) is { } b) b.SortOrder = i + 1;
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.PublicHome);
    }

    public async Task DeleteBannerAsync(int id, CancellationToken ct)
    {
        await db.Banners.Where(b => b.Id == id).ExecuteDeleteAsync(ct);
        await cache.RemoveAsync(CacheKeys.PublicHome);
    }

    // ---------------- Etkinlikler ----------------
    public Task<List<EventDto>> EventsAsync(UserContext me, bool upcomingOnly, CancellationToken ct)
    {
        var now = Now.AddHours(-3);
        var q = db.Events.AsNoTracking().AsQueryable();
        if (upcomingOnly) q = q.Where(e => (e.EndsAt ?? e.StartsAt) >= now);
        return q.OrderBy(e => e.StartsAt).Take(100).Select(e => new EventDto(e.Id, e.Title, e.Description, e.Location, e.Category, e.StartsAt, e.EndsAt, e.IsPublic,
            e.Attendees.Count, e.Attendees.Any(a => a.UserId == me.Id), new UserRef(e.CreatedBy.Id, e.CreatedBy.FirstName + " " + e.CreatedBy.LastName, e.CreatedBy.Email))).ToListAsync(ct);
    }

    public async Task<EventDto> SaveEventAsync(int? id, SaveEventRequest req, UserContext me, CancellationToken ct)
    {
        CompanyEvent e;
        if (id is { } x) e = await db.Events.FirstOrDefaultAsync(v => v.Id == x, ct) ?? throw AppException.NotFound("Etkinlik");
        else db.Events.Add(e = new CompanyEvent { Title = req.Title, CreatedById = me.Id });
        e.Title = req.Title.Trim(); e.Description = req.Description?.Trim(); e.Location = req.Location?.Trim(); e.Category = req.Category.Trim();
        e.StartsAt = req.StartsAt.ToUniversalTime(); e.EndsAt = req.EndsAt?.ToUniversalTime(); e.IsPublic = req.IsPublic;
        await db.SaveChangesAsync(ct);
        return (await EventsAsync(me, false, ct)).First(v => v.Id == e.Id);
    }

    public async Task<bool> ToggleAttendAsync(int id, UserContext me, CancellationToken ct)
    {
        if (!await db.Events.AnyAsync(e => e.Id == id, ct)) throw AppException.NotFound("Etkinlik");
        var existing = await db.EventAttendees.FirstOrDefaultAsync(a => a.EventId == id && a.UserId == me.Id, ct);
        if (existing is null) db.EventAttendees.Add(new EventAttendee { EventId = id, UserId = me.Id });
        else db.EventAttendees.Remove(existing);
        await db.SaveChangesAsync(ct);
        return existing is null;
    }

    public Task DeleteEventAsync(int id, CancellationToken ct) => db.Events.Where(e => e.Id == id).ExecuteDeleteAsync(ct);

    // ---------------- İş ilanları ----------------
    public Task<List<JobDto>> JobsAsync(bool activeOnly, bool publicOnly, CancellationToken ct)
    {
        var today = BusinessTime.DateOf(Now);
        var q = db.JobPostings.AsNoTracking().AsQueryable();
        if (activeOnly) q = q.Where(j => j.IsActive && (j.ClosesAt == null || j.ClosesAt >= today));
        if (publicOnly) q = q.Where(j => j.IsPublic);
        return q.OrderByDescending(j => j.CreatedAt).Select(j => new JobDto(j.Id, j.Title, j.Department == null ? null : new DeptRef(j.Department.Id, j.Department.Name),
            j.Location, j.EmploymentType, j.Description, j.Requirements, j.IsPublic, j.IsActive, j.ClosesAt, j.CreatedAt)).ToListAsync(ct);
    }

    public async Task<JobDto> SaveJobAsync(int? id, SaveJobRequest req, CancellationToken ct)
    {
        JobPosting j;
        if (id is { } e) j = await db.JobPostings.FirstOrDefaultAsync(x => x.Id == e, ct) ?? throw AppException.NotFound("İlan");
        else db.JobPostings.Add(j = new JobPosting { Title = req.Title, Description = req.Description });
        j.Title = req.Title.Trim(); j.DepartmentId = req.DepartmentId; j.Location = req.Location?.Trim(); j.EmploymentType = req.EmploymentType; j.Description = req.Description.Trim();
        j.Requirements = req.Requirements?.Trim(); j.IsPublic = req.IsPublic; j.IsActive = req.IsActive; j.ClosesAt = req.ClosesAt;
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.PublicHome);
        return (await JobsAsync(false, false, ct)).First(x => x.Id == j.Id);
    }

    public async Task DeleteJobAsync(int id, CancellationToken ct)
    {
        await db.JobPostings.Where(j => j.Id == id).ExecuteDeleteAsync(ct);
        await cache.RemoveAsync(CacheKeys.PublicHome);
    }

    // ---------------- Başarılar / takdir ----------------
    public Task<List<AchievementDto>> AchievementsAsync(int take, CancellationToken ct) =>
        db.Achievements.AsNoTracking().OrderByDescending(a => a.CreatedAt).Take(Math.Clamp(take, 1, 100)).Select(a => new AchievementDto(a.Id,
            new UserRef(a.User.Id, a.User.FirstName + " " + a.User.LastName, a.User.Email), a.User.JobTitle == null ? null : a.User.JobTitle.Name, a.Title, a.Description, a.Kind,
            new UserRef(a.GivenBy.Id, a.GivenBy.FirstName + " " + a.GivenBy.LastName, a.GivenBy.Email), a.Likes, a.CreatedAt)).ToListAsync(ct);

    public async Task<AchievementDto> GiveAsync(GiveKudosRequest req, UserContext me, CancellationToken ct)
    {
        if (req.UserId == me.Id) throw AppException.BadRequest("Kendinize takdir gönderemezsiniz.");
        if (req.Kind != AchievementKind.Kudos && !me.Has(Permissions.ContentManage)) throw AppException.Forbidden("Ödül ve kıdem rozetlerini yalnızca İK verebilir.");
        if (!await db.Users.AnyAsync(u => u.Id == req.UserId && u.IsActive, ct)) throw AppException.NotFound("Çalışan");
        var a = new Achievement { UserId = req.UserId, GivenById = me.Id, Title = req.Title.Trim(), Description = req.Description?.Trim(), Kind = req.Kind, CreatedAt = Now };
        db.Achievements.Add(a);
        events.Enqueue(new DirectNotificationEvent([req.UserId], "Tebrikler! 🎉", $"{me.Name} size takdir gönderdi: {a.Title}", "success", "/achievements") { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        return (await AchievementsAsync(100, ct)).First(x => x.Id == a.Id);
    }

    public async Task<int> LikeAsync(int id, CancellationToken ct)
    {
        await db.Achievements.Where(a => a.Id == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.Likes, a => a.Likes + 1), ct);
        return await db.Achievements.Where(a => a.Id == id).Select(a => a.Likes).FirstAsync(ct);
    }

    // ---------------- Anketler ----------------
    private static string RespondentHash(Guid userId, int surveyId) => DbSeeder.Hash($"{userId}:survey{surveyId}");

    public async Task<List<SurveyListItem>> SurveysAsync(UserContext me, CancellationToken ct)
    {
        var list = await db.Surveys.AsNoTracking().OrderByDescending(s => s.CreatedAt)
            .Select(s => new { s.Id, s.Title, s.Description, s.IsAnonymous, s.IsActive, s.EndsAt, s.Questions, Count = s.Responses.Count }).ToListAsync(ct);
        var hashes = list.Select(s => RespondentHash(me.Id, s.Id)).ToList();
        var responded = await db.SurveyResponses.Where(r => hashes.Contains(r.RespondentHash)).Select(r => r.SurveyId).ToListAsync(ct);
        return list.Where(s => s.IsActive || me.Has(Permissions.SurveysManage))
            .Select(s => new SurveyListItem(s.Id, s.Title, s.Description, s.IsAnonymous, s.IsActive && (s.EndsAt == null || s.EndsAt > Now), s.EndsAt, s.Questions.Count, s.Count, responded.Contains(s.Id))).ToList();
    }

    public async Task<SurveyDto> SurveyAsync(int id, UserContext me, CancellationToken ct)
    {
        var s = await db.Surveys.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Anket");
        var hash = RespondentHash(me.Id, id);
        return new SurveyDto(s.Id, s.Title, s.Description, s.IsAnonymous, s.IsActive && (s.EndsAt == null || s.EndsAt > Now), s.EndsAt, s.Questions,
            await db.SurveyResponses.AnyAsync(r => r.SurveyId == id && r.RespondentHash == hash, ct), me.Has(Permissions.SurveysManage));
    }

    public async Task<SurveyDto> SaveSurveyAsync(int? id, SaveSurveyRequest req, UserContext me, CancellationToken ct)
    {
        Survey s;
        if (id is { } e)
        {
            s = await db.Surveys.FirstOrDefaultAsync(x => x.Id == e, ct) ?? throw AppException.NotFound("Anket");
            if (await db.SurveyResponses.AnyAsync(r => r.SurveyId == e, ct) && s.Questions.Count != req.Questions.Count)
                throw AppException.BadRequest("Yanıt alınmış anketin soru sayısı değiştirilemez.");
        }
        else db.Surveys.Add(s = new Survey { Title = req.Title, CreatedById = me.Id });
        s.Title = req.Title.Trim(); s.Description = req.Description?.Trim(); s.IsAnonymous = req.IsAnonymous; s.IsActive = req.IsActive; s.EndsAt = req.EndsAt?.ToUniversalTime();
        s.Questions = req.Questions.Select((q, i) => new SurveyQuestion { Id = string.IsNullOrWhiteSpace(q.Id) ? $"q{i + 1}" : q.Id, Text = q.Text.Trim(), Type = q.Type, Options = q.Options, Required = q.Required }).ToList();
        var isNew = id is null;
        await db.SaveChangesAsync(ct);
        if (isNew && s.IsActive)
        {
            var all = await db.Users.Where(u => u.IsActive && u.Id != me.Id).Select(u => u.Id).ToListAsync(ct);
            events.Enqueue(new DirectNotificationEvent(all, "Yeni anket", s.Title, "info", $"/surveys/{s.Id}") { ActorId = me.Id, ActorName = me.Name });
            await db.SaveChangesAsync(ct);
        }
        return await SurveyAsync(s.Id, me, ct);
    }

    public async Task RespondAsync(int id, SurveyAnswerRequest req, UserContext me, CancellationToken ct)
    {
        var s = await db.Surveys.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Anket");
        if (!s.IsActive || (s.EndsAt is { } end && end < Now)) throw AppException.BadRequest("Bu anket yanıta kapalıdır.");
        var hash = RespondentHash(me.Id, id);
        if (await db.SurveyResponses.AnyAsync(r => r.SurveyId == id && r.RespondentHash == hash, ct)) throw AppException.Conflict("Bu ankete daha önce yanıt verdiniz.");
        var answers = new Dictionary<string, object?>();
        var errors = new Dictionary<string, string[]>();
        foreach (var q in s.Questions)
        {
            req.Answers.TryGetValue(q.Id, out var v);
            var empty = v.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null || (v.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(v.GetString())) || (v.ValueKind == JsonValueKind.Array && v.GetArrayLength() == 0);
            if (empty) { if (q.Required) errors[q.Id] = ["Bu soru zorunludur."]; continue; }
            switch (q.Type)
            {
                case "rating":
                    var n = v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
                    if (n is < 1 or > 5) errors[q.Id] = ["1 ile 5 arasında puan veriniz."]; else answers[q.Id] = n;
                    break;
                case "single":
                    var opt = v.GetString();
                    if (!q.Options.Contains(opt!)) errors[q.Id] = ["Geçersiz seçim."]; else answers[q.Id] = opt;
                    break;
                case "multi":
                    var opts = v.EnumerateArray().Select(x => x.GetString()!).Distinct().ToList();
                    if (opts.Any(o => !q.Options.Contains(o))) errors[q.Id] = ["Geçersiz seçim."]; else answers[q.Id] = opts;
                    break;
                default:
                    var text = v.GetString()!.Trim();
                    answers[q.Id] = text.Length > 2000 ? text[..2000] : text;
                    break;
            }
        }
        if (errors.Count > 0) throw new ValidationAppException(errors);
        db.SurveyResponses.Add(new SurveyResponse { SurveyId = id, UserId = s.IsAnonymous ? null : me.Id, RespondentHash = hash, Answers = JsonSerializer.Serialize(answers, Json), CreatedAt = Now });
        await db.SaveChangesAsync(ct);
    }

    public async Task<SurveyResults> ResultsAsync(int id, CancellationToken ct)
    {
        var s = await db.Surveys.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Anket");
        var responses = (await db.SurveyResponses.AsNoTracking().Where(r => r.SurveyId == id).Select(r => r.Answers).ToListAsync(ct))
            .Select(a => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(a, Json)!).ToList();
        var results = s.Questions.Select(q =>
        {
            var values = responses.Where(r => r.ContainsKey(q.Id)).Select(r => r[q.Id]).ToList();
            var counts = new Dictionary<string, int>();
            double? avg = null;
            var texts = new List<string>();
            switch (q.Type)
            {
                case "rating":
                    foreach (var v in values) counts[v.GetInt32().ToString()] = counts.GetValueOrDefault(v.GetInt32().ToString()) + 1;
                    avg = values.Count == 0 ? null : Math.Round(values.Average(v => v.GetInt32()), 2);
                    break;
                case "single":
                    foreach (var o in q.Options) counts[o] = values.Count(v => v.GetString() == o);
                    break;
                case "multi":
                    foreach (var o in q.Options) counts[o] = values.Count(v => v.EnumerateArray().Any(x => x.GetString() == o));
                    break;
                default:
                    texts = values.Select(v => v.GetString() ?? "").Where(t => t.Length > 0).Take(100).ToList();
                    break;
            }
            return new QuestionResult(q.Id, q.Text, q.Type, counts, avg, texts);
        }).ToList();
        return new SurveyResults(s.Id, s.Title, responses.Count, results);
    }

    // ---------------- Dokümanlar ----------------
    public Task<List<DocumentDto>> DocumentsAsync(UserContext me, string? category, string? search, CancellationToken ct)
    {
        var q = db.Documents.AsNoTracking().Where(d => d.DepartmentId == null || d.DepartmentId == me.DepartmentId || me.Has(Permissions.DocumentsManage));
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(d => d.Category == category);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(d => EF.Functions.ILike(d.Title, search.Like()) || (d.Description != null && EF.Functions.ILike(d.Description, search.Like())));
        return q.OrderBy(d => d.Category).ThenBy(d => d.Title).Select(d => new DocumentDto(d.Id, d.Title, d.Description, d.Category,
            new FileDto(d.File.Id, d.File.FileName, d.File.ContentType, d.File.Size, d.File.CreatedAt), d.Department == null ? null : new DeptRef(d.Department.Id, d.Department.Name),
            new UserRef(d.UploadedBy.Id, d.UploadedBy.FirstName + " " + d.UploadedBy.LastName, d.UploadedBy.Email), d.Downloads, d.CreatedAt)).ToListAsync(ct);
    }

    public async Task<DocumentDto> AddDocumentAsync(SaveDocumentRequest req, UserContext me, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) throw AppException.BadRequest("Başlık zorunludur.");
        if (!await db.Files.AnyAsync(f => f.Id == req.FileId && f.UploadedById == me.Id, ct)) throw AppException.BadRequest("Dosya bulunamadı. Lütfen yeniden yükleyiniz.");
        var d = new Document { Title = req.Title.Trim(), Description = req.Description?.Trim(), Category = req.Category.Trim(), FileId = req.FileId, DepartmentId = req.DepartmentId, UploadedById = me.Id };
        db.Documents.Add(d);
        await db.SaveChangesAsync(ct);
        return (await DocumentsAsync(me, null, null, ct)).First(x => x.Id == d.Id);
    }

    public Task DeleteDocumentAsync(int id, CancellationToken ct) => db.Documents.Where(d => d.Id == id).ExecuteDeleteAsync(ct);

    // ---------------- Mesajlaşma ----------------
    public async Task<List<ConversationDto>> ConversationsAsync(UserContext me, CancellationToken ct)
    {
        var msgs = await db.Messages.AsNoTracking().Where(m => m.SenderId == me.Id || m.RecipientId == me.Id).OrderByDescending(m => m.CreatedAt).Take(1000)
            .Select(m => new { m.SenderId, m.RecipientId, m.Body, m.CreatedAt, m.ReadAt }).ToListAsync(ct);
        var groups = msgs.GroupBy(m => m.SenderId == me.Id ? m.RecipientId : m.SenderId).ToList();
        var ids = groups.Select(g => g.Key).ToList();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName, u.Email, Title = u.JobTitle == null ? null : u.JobTitle.Name }).ToDictionaryAsync(u => u.Id, ct);
        return groups.Where(g => users.ContainsKey(g.Key)).Select(g =>
        {
            var last = g.First();
            var u = users[g.Key];
            return new ConversationDto(new UserRef(u.Id, u.Name, u.Email), u.Title, last.Body, last.CreatedAt, g.Count(m => m.RecipientId == me.Id && m.ReadAt == null), last.SenderId == me.Id);
        }).OrderByDescending(c => c.LastAt).ToList();
    }

    public async Task<List<MessageDto>> ThreadAsync(Guid otherId, UserContext me, CancellationToken ct)
    {
        await db.Messages.Where(m => m.SenderId == otherId && m.RecipientId == me.Id && m.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(m => m.ReadAt, Now), ct);
        return (await db.Messages.AsNoTracking().Where(m => (m.SenderId == me.Id && m.RecipientId == otherId) || (m.SenderId == otherId && m.RecipientId == me.Id))
            .OrderByDescending(m => m.CreatedAt).Take(200).Select(m => new MessageDto(m.Id, m.SenderId, m.Body, m.CreatedAt, m.ReadAt)).ToListAsync(ct)).OrderBy(m => m.CreatedAt).ToList();
    }

    public async Task<MessageDto> SendAsync(SendMessageRequest req, UserContext me, CancellationToken ct)
    {
        if (req.RecipientId == me.Id) throw AppException.BadRequest("Kendinize mesaj gönderemezsiniz.");
        if (!await db.Users.AnyAsync(u => u.Id == req.RecipientId && u.IsActive, ct)) throw AppException.NotFound("Alıcı");
        var m = new DirectMessage { SenderId = me.Id, RecipientId = req.RecipientId, Body = req.Body.Trim(), CreatedAt = Now };
        db.Messages.Add(m);
        events.Enqueue(new DirectNotificationEvent([req.RecipientId], "Yeni mesaj", $"{me.Name}: {(m.Body.Length > 80 ? m.Body[..80] + "…" : m.Body)}", "message", $"/messages?u={me.Id}") { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        return new MessageDto(m.Id, m.SenderId, m.Body, m.CreatedAt, null);
    }

    public Task<int> UnreadMessagesAsync(Guid userId, CancellationToken ct) => db.Messages.CountAsync(m => m.RecipientId == userId && m.ReadAt == null, ct);

    // ---------------- Projeler ve görevler ----------------
    public Task<List<ProjectDto>> ProjectsAsync(CancellationToken ct) =>
        db.Projects.AsNoTracking().OrderBy(p => p.Status).ThenBy(p => p.DueDate).Select(p => new ProjectDto(p.Id, p.Code, p.Name, p.Description,
            p.Department == null ? null : new DeptRef(p.Department.Id, p.Department.Name), new UserRef(p.Owner.Id, p.Owner.FirstName + " " + p.Owner.LastName, p.Owner.Email),
            p.Status, p.StartDate, p.DueDate, p.Tasks.Count, p.Tasks.Count(t => t.Status == WorkTaskStatus.Done))).ToListAsync(ct);

    public async Task<ProjectDto> SaveProjectAsync(int? id, SaveProjectRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Code)) throw AppException.BadRequest("Proje kodu ve adı zorunludur.");
        if (await db.Projects.AnyAsync(p => p.Code == req.Code && p.Id != id, ct)) throw AppException.Conflict("Bu proje kodu kullanılıyor.");
        Project p;
        if (id is { } e) p = await db.Projects.FirstOrDefaultAsync(x => x.Id == e, ct) ?? throw AppException.NotFound("Proje");
        else db.Projects.Add(p = new Project { Code = req.Code, Name = req.Name, OwnerId = req.OwnerId });
        p.Code = req.Code.Trim().ToUpperInvariant(); p.Name = req.Name.Trim(); p.Description = req.Description?.Trim(); p.DepartmentId = req.DepartmentId; p.OwnerId = req.OwnerId;
        p.Status = req.Status; p.StartDate = req.StartDate; p.DueDate = req.DueDate;
        await db.SaveChangesAsync(ct);
        return (await ProjectsAsync(ct)).First(x => x.Id == p.Id);
    }

    public Task<PagedResult<TaskDto>> TasksAsync(TaskQuery q, UserContext me, CancellationToken ct)
    {
        var today = BusinessTime.DateOf(Now);
        var query = db.Tasks.AsNoTracking().AsQueryable();
        query = q.Scope switch
        {
            "created" => query.Where(t => t.CreatedById == me.Id),
            "all" when me.Has(Permissions.ProjectsManage) => query,
            "all" => query.Where(t => t.AssigneeId == me.Id || t.CreatedById == me.Id || (t.Project != null && t.Project.OwnerId == me.Id)),
            _ => query.Where(t => t.AssigneeId == me.Id),
        };
        if (q.ProjectId is { } pid) query = query.Where(t => t.ProjectId == pid);
        if (q.Status is { } s) query = query.Where(t => t.Status == s);
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(t => EF.Functions.ILike(t.Title, q.Search.Like()));
        return query.OrderBy(t => t.Status == WorkTaskStatus.Done).ThenBy(t => t.DueDate).ToPagedAsync(q, t => new TaskDto(t.Id, t.ProjectId, t.Project == null ? null : t.Project.Name,
            t.Title, t.Description, t.Assignee == null ? null : new UserRef(t.Assignee.Id, t.Assignee.FirstName + " " + t.Assignee.LastName, t.Assignee.Email),
            new UserRef(t.CreatedBy.Id, t.CreatedBy.FirstName + " " + t.CreatedBy.LastName, t.CreatedBy.Email), t.Status, t.Priority, t.DueDate,
            t.Status != WorkTaskStatus.Done && t.DueDate != null && t.DueDate < today, t.CompletedAt, t.CreatedAt), ct);
    }

    public async Task<TaskDto> SaveTaskAsync(int? id, SaveTaskRequest req, UserContext me, CancellationToken ct)
    {
        WorkTask t;
        if (id is { } e)
        {
            t = await db.Tasks.Include(x => x.Project).FirstOrDefaultAsync(x => x.Id == e, ct) ?? throw AppException.NotFound("Görev");
            if (t.CreatedById != me.Id && t.AssigneeId != me.Id && t.Project?.OwnerId != me.Id && !me.Has(Permissions.ProjectsManage)) throw AppException.Forbidden();
        }
        else db.Tasks.Add(t = new WorkTask { Title = req.Title, CreatedById = me.Id });
        if (req.ProjectId is { } pid && !await db.Projects.AnyAsync(p => p.Id == pid, ct)) throw AppException.BadRequest("Proje bulunamadı.");
        var assigneeChanged = t.AssigneeId != req.AssigneeId;
        t.ProjectId = req.ProjectId; t.Title = req.Title.Trim(); t.Description = req.Description?.Trim(); t.AssigneeId = req.AssigneeId; t.Priority = req.Priority; t.DueDate = req.DueDate;
        SetStatus(t, req.Status);
        if (assigneeChanged && req.AssigneeId is { } a && a != me.Id)
            events.Enqueue(new DirectNotificationEvent([a], "Size görev atandı", t.Title, "info", "/tasks") { ActorId = me.Id, ActorName = me.Name });
        await db.SaveChangesAsync(ct);
        return (await TasksAsync(new TaskQuery { Scope = "all", PageSize = 200 }, me with { Permissions = new HashSet<string>(me.Permissions) { Permissions.ProjectsManage } }, ct)).Items.First(x => x.Id == t.Id);
    }

    public async Task MoveTaskAsync(int id, MoveTaskRequest req, UserContext me, CancellationToken ct)
    {
        var t = await db.Tasks.Include(x => x.Project).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Görev");
        if (t.AssigneeId != me.Id && t.CreatedById != me.Id && t.Project?.OwnerId != me.Id && !me.Has(Permissions.ProjectsManage)) throw AppException.Forbidden();
        SetStatus(t, req.Status);
        await db.SaveChangesAsync(ct);
    }

    private void SetStatus(WorkTask t, WorkTaskStatus s)
    {
        if (s == WorkTaskStatus.Done && t.Status != WorkTaskStatus.Done) t.CompletedAt = Now;
        if (s != WorkTaskStatus.Done) t.CompletedAt = null;
        t.Status = s;
    }

    public async Task DeleteTaskAsync(int id, UserContext me, CancellationToken ct)
    {
        var t = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Görev");
        if (t.CreatedById != me.Id && !me.Has(Permissions.ProjectsManage)) throw AppException.Forbidden();
        db.Tasks.Remove(t);
        await db.SaveChangesAsync(ct);
    }

    // ---------------- Mağazalar ----------------
    public Task<PagedResult<StoreDto>> StoresAsync(PagedQuery q, string? city, StoreStatus? status, CancellationToken ct)
    {
        var query = db.Stores.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(city)) query = query.Where(s => s.City == city);
        if (status is { } st) query = query.Where(s => s.Status == st);
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(s => EF.Functions.ILike(s.Name, q.Search.Like()) || EF.Functions.ILike(s.Code, q.Search.Like()));
        return query.OrderBy(s => s.Code).ToPagedAsync(q, s => new StoreDto(s.Id, s.Code, s.Name, s.City, s.Department == null ? null : new DeptRef(s.Department.Id, s.Department.Name),
            s.Manager == null ? null : new UserRef(s.Manager.Id, s.Manager.FirstName + " " + s.Manager.LastName, s.Manager.Email), s.Phone, s.Address, s.Status, s.AreaSqm,
            s.Department == null ? 0 : s.Department.Users.Count(u => u.IsActive)), ct);
    }

    public Task<List<string>> StoreCitiesAsync(CancellationToken ct) => db.Stores.Where(s => s.City != null).Select(s => s.City!).Distinct().OrderBy(c => c).ToListAsync(ct);

    public async Task<StoreDto> SaveStoreAsync(int id, SaveStoreRequest req, CancellationToken ct)
    {
        var s = await db.Stores.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw AppException.NotFound("Mağaza");
        if (await db.Stores.AnyAsync(x => x.Code == req.Code && x.Id != id, ct)) throw AppException.Conflict("Bu mağaza kodu kullanılıyor.");
        s.Code = req.Code.Trim().ToUpperInvariant(); s.Name = req.Name.Trim(); s.City = req.City?.Trim(); s.ManagerId = req.ManagerId; s.Phone = req.Phone?.Trim();
        s.Address = req.Address?.Trim(); s.Status = req.Status; s.AreaSqm = req.AreaSqm;
        await db.SaveChangesAsync(ct);
        return (await StoresAsync(new PagedQuery { Search = s.Code, PageSize = 5 }, null, null, ct)).Items.First(x => x.Id == id);
    }

    // ---------------- Nöbetçi kişi ----------------
    public Task<List<OnCallDto>> OnCallAsync(bool currentOnly, CancellationToken ct)
    {
        var now = Now;
        var q = db.OnCallShifts.AsNoTracking().Where(s => s.EndsAt >= now.AddDays(-7));
        if (currentOnly) q = q.Where(s => s.StartsAt <= now && s.EndsAt >= now);
        return q.OrderBy(s => s.StartsAt).ThenBy(s => s.Team).Select(s => new OnCallDto(s.Id, new UserRef(s.User.Id, s.User.FirstName + " " + s.User.LastName, s.User.Email),
            s.User.Phone, s.User.JobTitle == null ? null : s.User.JobTitle.Name, s.Team, s.StartsAt, s.EndsAt, s.Note, s.StartsAt <= now && s.EndsAt >= now)).ToListAsync(ct);
    }

    public async Task<OnCallDto> AddOnCallAsync(SaveOnCallRequest req, CancellationToken ct)
    {
        if (await db.OnCallShifts.AnyAsync(s => s.Team == req.Team && s.StartsAt < req.EndsAt.ToUniversalTime() && s.EndsAt > req.StartsAt.ToUniversalTime(), ct))
            throw AppException.Conflict("Bu ekip için seçilen aralıkta başka bir nöbet tanımlı.");
        var s = new OnCallShift { UserId = req.UserId, Team = req.Team.Trim(), StartsAt = req.StartsAt.ToUniversalTime(), EndsAt = req.EndsAt.ToUniversalTime(), Note = req.Note?.Trim() };
        db.OnCallShifts.Add(s);
        await db.SaveChangesAsync(ct);
        return (await OnCallAsync(false, ct)).First(x => x.Id == s.Id);
    }

    public Task DeleteOnCallAsync(int id, CancellationToken ct) => db.OnCallShifts.Where(s => s.Id == id).ExecuteDeleteAsync(ct);

    // ---------------- Bildirimler ----------------
    public Task<List<NotificationDto>> NotificationsAsync(Guid userId, bool unreadOnly, int take, CancellationToken ct) =>
        db.Notifications.AsNoTracking().Where(n => n.UserId == userId && (!unreadOnly || !n.IsRead)).OrderByDescending(n => n.CreatedAt).Take(Math.Clamp(take, 1, 200))
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.Kind, n.Link, n.IsRead, n.CreatedAt)).ToListAsync(ct);

    public Task<int> UnreadNotificationsAsync(Guid userId, CancellationToken ct) => db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public Task MarkReadAsync(Guid userId, long? id, CancellationToken ct) =>
        db.Notifications.Where(n => n.UserId == userId && !n.IsRead && (id == null || n.Id == id))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, Now), ct);
}
