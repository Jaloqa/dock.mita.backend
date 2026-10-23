using FluentValidation;
using FluentValidation.Results;
using Mita.Api.Common.Groups;
using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;

namespace Mita.Api.Features.Groups.UpdateGroup;

public sealed class UpdateGroupValidator : AbstractValidator<UpdateGroupCommand>
{
    public UpdateGroupValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Name).Custom((value, context) =>
        {
            if (GroupNames.Error(value) is { } code)
            {
                context.AddFailure(new ValidationFailure(nameof(UpdateGroupCommand.Name), code) { ErrorCode = code });
            }
        });

        RuleFor(command => command.Currency)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(GroupNames.Required)
            .Must(value => CurrencyWire.TryParse(value, out _)).WithErrorCode(GroupNames.InvalidValue);
    }
}
