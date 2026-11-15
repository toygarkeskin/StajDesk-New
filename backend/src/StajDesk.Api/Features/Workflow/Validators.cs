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
