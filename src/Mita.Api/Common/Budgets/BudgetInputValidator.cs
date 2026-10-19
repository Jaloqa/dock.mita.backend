using FluentValidation;
using Mita.Api.Common.Wire;
using Mita.Domain.Money;

namespace Mita.Api.Common.Budgets;

public abstract class BudgetInputValidator<T> : AbstractValidator<T>
    where T : IBudgetInput
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string PositiveAmount = "validation.positiveAmount";

    protected BudgetInputValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(input => input.Period)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetPeriodKey(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(input => input.CategoryId)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetGuid(value, out _)).WithErrorCode(BudgetReferenceRules.UnknownCategory);

        RuleFor(input => input.Limit)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetSafeInteger(value, out var limit) && limit <= MinorUnits.MaxAmount).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetSafeInteger(value, out var limit) && limit > 0).WithErrorCode(PositiveAmount);
    }
}
