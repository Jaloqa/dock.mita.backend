using FluentValidation;
using FluentValidation.Results;
using Mita.Api.Common.Tags;

namespace Mita.Api.Features.Tags.RenameTag;

public sealed class RenameTagValidator : AbstractValidator<RenameTagCommand>
{
    public RenameTagValidator() =>
        RuleFor(command => command.Name).Custom((value, context) =>
        {
            if (TagNames.NameError(value) is { } code)
            {
                context.AddFailure(new ValidationFailure(nameof(RenameTagCommand.Name), code) { ErrorCode = code });
            }
        });
}
