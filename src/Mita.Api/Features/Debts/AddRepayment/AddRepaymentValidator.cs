using FluentValidation;
using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Time;
using Mita.Api.Common.Transactions;
using Mita.Api.Common.Wire;
using Mita.Domain.Debts;
using Mita.Domain.Money;

namespace Mita.Api.Features.Debts.AddRepayment;

public sealed class AddRepaymentValidator : AbstractValidator<AddRepaymentCommand>
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string PositiveAmount = "validation.positiveAmount";
    private const string DateTooFar = "validation.dateTooFar";
    private const string NoteTooLong = "validation.noteTooLong";

    public AddRepaymentValidator(TimeProvider time)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Amount)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetSafeInteger(value, out var amount) && amount <= MinorUnits.MaxAmount).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetSafeInteger(value, out var amount) && amount > 0).WithErrorCode(PositiveAmount);

        RuleFor(command => command.Currency)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => CurrencyWire.TryParse(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(command => command.Date)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetIsoDate(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetIsoDate(value, out var date) && date <= UtcCalendar.LatestAllowedDate(time))
            .WithErrorCode(DateTooFar);

        RuleFor(command => command.Rate)
            .Must(value => ManualRate.TryParse(value, out _)).WithErrorCode(InvalidValue)
            .When(command => !JsonValues.IsMissing(command.Rate));

        RuleFor(command => command.AccountId)
            .Must(value => JsonValues.TryGetGuid(value, out _)).WithErrorCode(TransactionReferenceRules.UnknownAccount)
            .When(command => !JsonValues.IsMissing(command.AccountId));

        RuleFor(command => command.Note)
            .Must(value => JsonValues.TryGetOptionalText(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetOptionalText(value, out var text) && (text is null || text.Length <= Debt.NoteMaxLength))
            .WithErrorCode(NoteTooLong)
            .When(command => !JsonValues.IsMissing(command.Note));
    }
}
