using FluentValidation;

namespace Mita.Api.Features.Users.DeleteMe;

public sealed class DeleteMeValidator : AbstractValidator<DeleteMeCommand>
{
    public DeleteMeValidator()
    {
        RuleFor(command => command.Password)
            .Must(password => !string.IsNullOrEmpty(password)).WithErrorCode("validation.required");
    }
}
