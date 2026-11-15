using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public class CreateTicketValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Başlık zorunludur.").Length(5, 200).WithMessage("Başlık 5-200 karakter olmalıdır.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Açıklama zorunludur.").Length(10, 8000).WithMessage("Açıklama en az 10 karakter olmalıdır.");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Kategori seçiniz.");
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.AttachmentIds).Must(a => a is null || a.Count <= 5).WithMessage("En fazla 5 dosya eklenebilir.");
    }
}

public class UpdateTicketValidator : AbstractValidator<UpdateTicketRequest>
{
    public UpdateTicketValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(5, 200).WithMessage("Başlık 5-200 karakter olmalıdır.");
        RuleFor(x => x.Description).NotEmpty().Length(10, 8000).WithMessage("Açıklama en az 10 karakter olmalıdır.");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Kategori seçiniz.");
    }
}

public class AddCommentValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentValidator() => RuleFor(x => x.Body).NotEmpty().WithMessage("Yorum boş olamaz.").MaximumLength(4000);
}

public class SaveCategoryValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Kategori adı zorunludur.").MaximumLength(80);
        RuleFor(x => x.Color).Matches("^#[0-9a-fA-F]{6}$").WithMessage("Renk #RRGGBB formatında olmalıdır.");
    }
}

public class SaveRoutingRuleValidator : AbstractValidator<SaveRoutingRuleRequest>
{
    public SaveRoutingRuleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Kural adı zorunludur.").MaximumLength(120);
        RuleFor(x => x).Must(x => x.AssignGroupId.HasValue || x.AssignUserId.HasValue || x.SetPriority.HasValue)
            .WithMessage("Kural en az bir eylem içermelidir (grup, kişi veya öncelik).").OverridePropertyName("assignGroupId");
    }
}

public class SaveArticleValidator : AbstractValidator<SaveArticleRequest>
{
    public SaveArticleValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(5, 200).WithMessage("Başlık 5-200 karakter olmalıdır.");
        RuleFor(x => x.Summary).NotEmpty().Length(10, 400).WithMessage("Özet 10-400 karakter olmalıdır.");
        RuleFor(x => x.Content).NotEmpty().Length(20, 20000).WithMessage("İçerik en az 20 karakter olmalıdır.");
    }
}
