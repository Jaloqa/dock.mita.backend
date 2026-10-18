using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Users;
using Mita.Infrastructure.Identity;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Auth.Register;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public const int PasswordMaxLength = 128;

    public RegisterValidator(AppDbContext db, ILookupNormalizer normalizer)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode("validation.required")
            .Must(name => name!.Trim().Length <= AppUser.NameMaxLength).WithErrorCode("validation.nameTooLong")
            .Must(name => !name!.Trim().Any(char.IsControl)).WithErrorCode("validation.invalidValue");

        RuleFor(command => command.Email)
            .Must(email => !string.IsNullOrWhiteSpace(email)).WithErrorCode("validation.required")
            .Must(email => EmailAddress.IsValid(email!.Trim())).WithErrorCode("validation.invalidEmail")
            .MustAsync(async (email, ct) =>
                {
                    var normalized = normalizer.NormalizeEmail(email!.Trim());
                    return !await db.Users.AnyAsync(user => user.NormalizedEmail == normalized, ct);
                })
            .WithErrorCode("validation.emailTaken");

        RuleFor(command => command.Password)
            .Must(password => !string.IsNullOrWhiteSpace(password)).WithErrorCode("validation.required")
            .Must(password => password!.Length >= IdentitySetup.PasswordMinLength).WithErrorCode("validation.passwordTooShort")
            .Must(password => password!.Length <= PasswordMaxLength).WithErrorCode("validation.passwordTooLong");
    }
}
