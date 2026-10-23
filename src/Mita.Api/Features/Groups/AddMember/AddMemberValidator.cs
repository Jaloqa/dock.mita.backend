using FluentValidation;
using FluentValidation.Results;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.AddMember;

public sealed class AddMemberValidator : AbstractValidator<AddMemberCommand>
{
    public AddMemberValidator() =>
        RuleFor(command => command.Name).Custom((value, context) =>
        {
            if (GroupNames.Error(value) is { } code)
            {
                context.AddFailure(new ValidationFailure(nameof(AddMemberCommand.Name), code) { ErrorCode = code });
            }
        });
}
