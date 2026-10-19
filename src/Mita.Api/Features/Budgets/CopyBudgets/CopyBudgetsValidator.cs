using FluentValidation;
using Mita.Api.Common.Wire;

namespace Mita.Api.Features.Budgets.CopyBudgets;

public sealed class CopyBudgetsValidator : AbstractValidator<CopyBudgetsCommand>
{
    public CopyBudgetsValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Period)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode("validation.required")
            .Must(value => JsonValues.TryGetPeriodKey(value, out _)).WithErrorCode("validation.invalidValue");
    }
}
