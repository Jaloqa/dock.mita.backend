using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Mita.Infrastructure.Identity;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Auth.Register;

public sealed partial class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public const int EmailMaxLength = 254;
    public const int PasswordMaxLength = 128;

    public RegisterValidator(AppDbContext db, ILookupNormalizer normalizer)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode("validation.required")
            .Must(name => name!.Trim().Length <= AppUser.NameMaxLength).WithErrorCode("validation.nameTooLong");

        RuleFor(command => command.Email)
            .Must(email => !string.IsNullOrWhiteSpace(email)).WithErrorCode("validation.required")
            .Must(email => IsValidEmail(email!.Trim())).WithErrorCode("validation.invalidEmail")
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

    public static bool IsValidEmail(string email) => email.Length <= EmailMaxLength && EmailPattern().IsMatch(email);

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailPattern();
}
