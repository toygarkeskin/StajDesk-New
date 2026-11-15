using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public class SaveAssetValidator : AbstractValidator<SaveAssetRequest>
{
    public SaveAssetValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Varlık adı zorunludur.").MaximumLength(150);
        RuleFor(x => x.AssetTag).NotEmpty().WithMessage("Envanter numarası zorunludur.").Matches("^[A-Za-z0-9-]{3,40}$").WithMessage("Envanter numarası harf, rakam ve tire içerebilir.");
        RuleFor(x => x.PurchaseCost).GreaterThanOrEqualTo(0).When(x => x.PurchaseCost.HasValue).WithMessage("Tutar negatif olamaz.");
        RuleFor(x => x.WarrantyEndsAt).GreaterThanOrEqualTo(x => x.PurchaseDate).When(x => x.PurchaseDate.HasValue && x.WarrantyEndsAt.HasValue).WithMessage("Garanti bitişi satın alma tarihinden önce olamaz.");
    }
}

public class SaveLicenseValidator : AbstractValidator<SaveLicenseRequest>
{
    public SaveLicenseValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Lisans adı zorunludur.").MaximumLength(150);
        RuleFor(x => x.Seats).InclusiveBetween(1, 100000).WithMessage("Lisans adedi 1 ile 100.000 arasında olmalıdır.");
    }
}
