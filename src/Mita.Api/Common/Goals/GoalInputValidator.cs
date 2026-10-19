using FluentValidation;
using Mita.Api.Common.Wire;
using Mita.Domain.Goals;
using Mita.Domain.Money;

namespace Mita.Api.Common.Goals;

public abstract class GoalInputValidator<T> : AbstractValidator<T>
    where T : IGoalInput
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string PositiveAmount = "validation.positiveAmount";
    private const string NameTooLong = "validation.nameTooLong";

    protected GoalInputValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(input => input.Name)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => GoalInputParser.TryParseName(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => GoalInputParser.TryParseName(value, out var name) && name.Length <= Goal.NameMaxLength)
            .WithErrorCode(NameTooLong);

        RuleFor(input => input.Target)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetSafeInteger(value, out var target) && target <= MinorUnits.MaxAmount).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetSafeInteger(value, out var target) && target > 0).WithErrorCode(PositiveAmount);

        RuleFor(input => input.Deadline)
            .Must(value => JsonValues.TryGetPeriodKey(value, out _))
            .When(input => !JsonValues.IsMissing(input.Deadline))
            .WithErrorCode(InvalidValue);

        RuleFor(input => input.AccountId)
            .Must(value => JsonValues.TryGetGuid(value, out _))
            .When(input => !JsonValues.IsMissing(input.AccountId))
            .WithErrorCode(GoalReferenceRules.UnknownAccount);
    }
}
