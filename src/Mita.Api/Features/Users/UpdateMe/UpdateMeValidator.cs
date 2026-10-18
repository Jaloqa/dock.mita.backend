using FluentValidation;
using Mita.Api.Common.Users;
using Mita.Infrastructure.Identity;

namespace Mita.Api.Features.Users.UpdateMe;

public sealed class UpdateMeValidator : AbstractValidator<UpdateMeCommand>
{
    public UpdateMeValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithErrorCode("validation.required")
            .Must(name => name!.Trim().Length <= AppUser.NameMaxLength).WithErrorCode("validation.nameTooLong")
            .Must(name => !name!.Trim().Any(char.IsControl)).WithErrorCode("validation.invalidValue");

        RuleFor(command => command.Email)
            .Must(email => !string.IsNullOrWhiteSpace(email)).WithErrorCode("validation.required")
            .Must(email => EmailAddress.IsValid(email!.Trim())).WithErrorCode("validation.invalidEmail");
    }
}
