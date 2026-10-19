using FluentValidation;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Time;
using Mita.Api.Common.Wire;
using Mita.Domain.Money;

namespace Mita.Api.Features.Goals.ContributeToGoal;

public sealed class ContributeToGoalValidator : AbstractValidator<ContributeToGoalCommand>
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string DateTooFar = "validation.dateTooFar";
    private const string PositiveAmount = "validation.positiveAmount";

    public ContributeToGoalValidator(TimeProvider time)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Amount)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetSafeInteger(value, out var amount) && amount <= MinorUnits.MaxAmount).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetSafeInteger(value, out var amount) && amount > 0).WithErrorCode(PositiveAmount);

        RuleFor(command => command.Date)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetIsoDate(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetIsoDate(value, out var date) && date <= UtcCalendar.LatestAllowedDate(time))
            .WithErrorCode(DateTooFar);

        RuleFor(command => command.Rate)
            .Must(value => ManualRate.TryParse(value, out _)).WithErrorCode(InvalidValue)
            .When(command => !JsonValues.IsMissing(command.Rate));

        RuleFor(command => command.FromAccountId)
            .Must(value => JsonValues.TryGetGuid(value, out _)).WithErrorCode(ContributionAccountRules.UnknownAccount)
            .When(command => !JsonValues.IsMissing(command.FromAccountId));
    }
}
