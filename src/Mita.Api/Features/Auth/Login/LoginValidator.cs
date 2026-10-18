using FluentValidation;

namespace Mita.Api.Features.Auth.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Email)
            .Must(email => !string.IsNullOrWhiteSpace(email)).WithErrorCode("validation.required")
            .Must(email => !email!.Trim().Any(char.IsControl)).WithErrorCode("validation.invalidEmail");

        RuleFor(command => command.Password)
            .Must(password => !string.IsNullOrEmpty(password)).WithErrorCode("validation.required");
    }
}
