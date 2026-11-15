using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StajDesk.Api.Common;
using StajDesk.Domain;
using StajDesk.Infrastructure.Data;
using StajDesk.Infrastructure.Email;

namespace StajDesk.Api.Features;

public class VerifyLoginCodeValidator : AbstractValidator<VerifyLoginCodeRequest>
{
    public VerifyLoginCodeValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().WithMessage("Doğrulama kodunu giriniz.")
            .Matches("^[0-9]{4,8}$").WithMessage("Doğrulama kodu yalnızca rakamlardan oluşmalıdır.");
    }
}
