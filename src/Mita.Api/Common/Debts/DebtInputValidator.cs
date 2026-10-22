using FluentValidation;
using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Time;
using Mita.Api.Common.Transactions;
using Mita.Api.Common.Wire;
using Mita.Domain.Debts;
using Mita.Domain.Money;

namespace Mita.Api.Common.Debts;

public abstract class DebtInputValidator<T> : AbstractValidator<T>
    where T : IDebtInput
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string NameTooLong = "validation.nameTooLong";
    private const string PositiveAmount = "validation.positiveAmount";
    private const string DateTooFar = "validation.dateTooFar";
    private const string DueBeforeDate = "validation.dueBeforeDate";
    private const string NoteTooLong = "validation.noteTooLong";

    protected DebtInputValidator(TimeProvider time)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(input => input.Direction)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => DebtWire.TryParseDirection(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(input => input.Counterparty)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetOptionalText(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetOptionalText(value, out var text) && text?.Length <= Debt.CounterpartyMaxLength)
            .WithErrorCode(NameTooLong);

        RuleFor(input => input.Amount)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetSafeInteger(value, out var amount) && amount <= MinorUnits.MaxAmount).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetSafeInteger(value, out var amount) && amount > 0).WithErrorCode(PositiveAmount);

        RuleFor(input => input.Currency)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => CurrencyWire.TryParse(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(input => input.Date)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetIsoDate(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetIsoDate(value, out var date) && date <= UtcCalendar.LatestAllowedDate(time))
            .WithErrorCode(DateTooFar);

        RuleFor(input => input.DueDate)
            .Must(value => JsonValues.TryGetIsoDate(value, out _)).WithErrorCode(InvalidValue)
            .Must((input, value) => !JsonValues.TryGetIsoDate(input.Date, out var date)
                || (JsonValues.TryGetIsoDate(value, out var dueDate) && dueDate >= date))
            .WithErrorCode(DueBeforeDate)
            .When(input => !JsonValues.IsMissing(input.DueDate));

        RuleFor(input => input.AccountId)
            .Must(value => JsonValues.TryGetGuid(value, out _)).WithErrorCode(TransactionReferenceRules.UnknownAccount)
            .When(input => !JsonValues.IsMissing(input.AccountId));

        RuleFor(input => input.Rate)
            .Must(value => ManualRate.TryParse(value, out _)).WithErrorCode(InvalidValue)
            .When(input => !JsonValues.IsMissing(input.Rate));

        RuleFor(input => input.Note)
            .Must(value => JsonValues.TryGetOptionalText(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetOptionalText(value, out var text) && (text is null || text.Length <= Debt.NoteMaxLength))
            .WithErrorCode(NoteTooLong)
            .When(input => !JsonValues.IsMissing(input.Note));
    }
}
