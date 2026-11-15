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

public class SaveAnnouncementValidator : AbstractValidator<SaveAnnouncementRequest>
{
    public SaveAnnouncementValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlık zorunludur.").MaximumLength(200);
        RuleFor(x => x.Summary).NotEmpty().WithMessage("Özet zorunludur.").MaximumLength(400);
        RuleFor(x => x.Body).NotEmpty().WithMessage("İçerik zorunludur.").MaximumLength(10000);
        RuleFor(x => x.ExpiresAt).GreaterThan(x => x.PublishAt ?? DateTime.UtcNow).When(x => x.ExpiresAt.HasValue).WithMessage("Bitiş tarihi yayın tarihinden sonra olmalıdır.");
    }
}

public class SaveBannerValidator : AbstractValidator<SaveBannerRequest>
{
    private static readonly string[] Themes = ["crimson", "midnight", "sunset", "ocean"];
    public SaveBannerValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlık zorunludur.").MaximumLength(150);
        RuleFor(x => x.Theme).Must(t => Themes.Contains(t)).WithMessage("Geçersiz tema.");
        RuleFor(x => x.ImageUrl).Must(u => u is null || u.StartsWith("https://") || u.StartsWith("/")).WithMessage("Görsel adresi https:// ile başlamalıdır.");
        RuleFor(x => x.LinkUrl).Must(u => u is null || u.StartsWith("https://") || u.StartsWith("/") || u.StartsWith("#")).WithMessage("Bağlantı https://, / veya # ile başlamalıdır.");
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).When(x => x.StartsAt.HasValue && x.EndsAt.HasValue).WithMessage("Bitiş tarihi başlangıçtan sonra olmalıdır.");
    }
}

public class SaveEventValidator : AbstractValidator<SaveEventRequest>
{
    public SaveEventValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Etkinlik adı zorunludur.").MaximumLength(150);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).When(x => x.EndsAt.HasValue).WithMessage("Bitiş başlangıçtan sonra olmalıdır.");
    }
}

public class SaveJobValidator : AbstractValidator<SaveJobRequest>
{
    public SaveJobValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Pozisyon adı zorunludur.").MaximumLength(150);
        RuleFor(x => x.Description).NotEmpty().WithMessage("Açıklama zorunludur.").MaximumLength(5000);
    }
}

public class GiveKudosValidator : AbstractValidator<GiveKudosRequest>
{
    public GiveKudosValidator() => RuleFor(x => x.Title).NotEmpty().WithMessage("Başlık zorunludur.").MaximumLength(150);
}

public class SaveSurveyValidator : AbstractValidator<SaveSurveyRequest>
{
    public SaveSurveyValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Anket başlığı zorunludur.").MaximumLength(200);
        RuleFor(x => x.Questions).NotEmpty().WithMessage("En az bir soru ekleyiniz.");
        RuleForEach(x => x.Questions).ChildRules(q =>
        {
            q.RuleFor(x => x.Text).NotEmpty().WithMessage("Soru metni zorunludur.");
            q.RuleFor(x => x.Type).Must(t => t is "single" or "multi" or "text" or "rating").WithMessage("Geçersiz soru tipi.");
            q.RuleFor(x => x.Options).Must(o => o.Count >= 2).When(x => x.Type is "single" or "multi").WithMessage("Seçenekli sorularda en az iki seçenek olmalıdır.");
        });
    }
}

public class SaveTaskValidator : AbstractValidator<SaveTaskRequest>
{
    public SaveTaskValidator() => RuleFor(x => x.Title).NotEmpty().WithMessage("Görev başlığı zorunludur.").MaximumLength(200);
}

public class SendMessageValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageValidator() => RuleFor(x => x.Body).NotEmpty().WithMessage("Mesaj boş olamaz.").MaximumLength(4000);
}

public class SaveOnCallValidator : AbstractValidator<SaveOnCallRequest>
{
    public SaveOnCallValidator()
    {
        RuleFor(x => x.Team).NotEmpty().WithMessage("Ekip zorunludur.");
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).WithMessage("Bitiş başlangıçtan sonra olmalıdır.");
    }
}
