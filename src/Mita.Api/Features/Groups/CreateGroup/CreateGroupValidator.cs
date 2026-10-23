using FluentValidation;
using FluentValidation.Results;
using Mita.Api.Common.Groups;
using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;

namespace Mita.Api.Features.Groups.CreateGroup;

public sealed class CreateGroupValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Name).Custom((value, context) =>
        {
            if (GroupNames.Error(value) is { } code)
            {
                context.AddFailure(new ValidationFailure(nameof(CreateGroupCommand.Name), code) { ErrorCode = code });
            }
        });

        RuleFor(command => command.Currency)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(GroupNames.Required)
            .Must(value => CurrencyWire.TryParse(value, out _)).WithErrorCode(GroupNames.InvalidValue);

        RuleFor(command => command.Members).Custom((value, context) =>
        {
            if (GroupMemberNames.Error(value) is { } code)
            {
                context.AddFailure(new ValidationFailure(nameof(CreateGroupCommand.Members), code) { ErrorCode = code });
            }
        });
    }
}
