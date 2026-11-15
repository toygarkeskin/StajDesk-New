using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Messaging;

namespace StajDesk.Api.Features;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("E-posta zorunludur.").EmailAddress().WithMessage("Geçerli bir e-posta adresi giriniz.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Parola zorunludur.");
    }
}

public class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Mevcut parolanızı giriniz.");
        RuleFor(x => x.NewPassword).Must(PasswordPolicy.IsValid).WithMessage(PasswordPolicy.Message)
            .NotEqual(x => x.CurrentPassword).WithMessage("Yeni parola mevcut paroladan farklı olmalıdır.");
    }
}

public class SaveUserValidator : AbstractValidator<SaveUserRequest>
{
    public SaveUserValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("Ad zorunludur.").MaximumLength(60);
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Soyad zorunludur.").MaximumLength(60);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Geçerli bir kurumsal e-posta adresi giriniz.").MaximumLength(200);
        RuleFor(x => x.DepartmentId).NotNull().WithMessage("Departman seçiniz.");
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.BirthDate).LessThan(DateOnly.FromDateTime(DateTime.Today.AddYears(-15))).When(x => x.BirthDate.HasValue).WithMessage("Doğum tarihi geçersiz.");
    }
}

public class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("Ad zorunludur.").MaximumLength(60);
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Soyad zorunludur.").MaximumLength(60);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Geçerli bir kurumsal e-posta adresi giriniz.");
        RuleFor(x => x.DepartmentId).GreaterThan(0).WithMessage("Departman seçiniz.");
        RuleFor(x => x.StartDate).GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Today.AddDays(-30))).WithMessage("Başlangıç tarihi geçmişte olamaz.");
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public class SaveRoleValidator : AbstractValidator<SaveRoleRequest>
{
    public SaveRoleValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Matches("^[A-Z_]{2,40}$").WithMessage("Rol kodu büyük harf ve alt çizgiden oluşmalıdır (ör. BOLGE_MUDURU).");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleForEach(x => x.Permissions).Must(p => Permissions.All.Any(a => a.Code == p)).WithMessage("Tanımsız izin kodu.");
    }
}

public class SaveDepartmentValidator : AbstractValidator<SaveDepartmentRequest>
{
    public SaveDepartmentValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Departman adı zorunludur.").MaximumLength(150);
}

public class SaveGroupValidator : AbstractValidator<SaveGroupRequest>
{
    public SaveGroupValidator() => RuleFor(x => x.Name).NotEmpty().WithMessage("Grup adı zorunludur.").MaximumLength(100);
}
