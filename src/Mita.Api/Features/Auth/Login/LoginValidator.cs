using FluentValidation;

namespace Mita.Api.Features.Auth.Login;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(command => command.Email)
            .Must(email => !string.IsNullOrWhiteSpace(email)).WithErrorCode("validation.required");

        RuleFor(command => command.Password)
            .Must(password => !string.IsNullOrEmpty(password)).WithErrorCode("validation.required");
    }
}
