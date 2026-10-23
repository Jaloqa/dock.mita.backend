using FluentValidation;
using FluentValidation.Results;
using Mita.Api.Common.Groups;

namespace Mita.Api.Features.Groups.RenameMember;

public sealed class RenameMemberValidator : AbstractValidator<RenameMemberCommand>
{
    public RenameMemberValidator() =>
        RuleFor(command => command.Name).Custom((value, context) =>
        {
            if (GroupNames.Error(value) is { } code)
            {
                context.AddFailure(new ValidationFailure(nameof(RenameMemberCommand.Name), code) { ErrorCode = code });
            }
        });
}
