using System.Text.Json;
using FluentValidation;
using Mita.Api.Common.Categories;
using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Time;
using Mita.Api.Common.Wire;
using Mita.Domain.Categories;
using Mita.Domain.Money;
using Mita.Domain.Transactions;

namespace Mita.Api.Common.Transactions;

public abstract class TransactionInputValidator<T> : AbstractValidator<T>
    where T : ITransactionInput
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string DateTooFar = "validation.dateTooFar";
    private const string TitleTooLong = "validation.titleTooLong";
    private const string NoteTooLong = "validation.noteTooLong";
    private const string PositiveAmount = "validation.positiveAmount";

    protected TransactionInputValidator(TimeProvider time)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(input => input.Kind)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => CategoryWire.TryParse(value, out CategoryKind _)).WithErrorCode(InvalidValue);

        RuleFor(input => input.Date)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetIsoDate(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetIsoDate(value, out var date) && date <= UtcCalendar.LatestAllowedDate(time))
            .WithErrorCode(DateTooFar);

        RuleFor(input => input.Title)
            .Must(value => JsonValues.TryGetString(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => Trimmed(value).Length <= Transaction.TitleMaxLength).WithErrorCode(TitleTooLong)
            .Must(value => JsonValues.TryGetOptionalText(value, out _)).WithErrorCode(InvalidValue)
            .When(input => !JsonValues.IsMissing(input.Title));

        RuleFor(input => input.Note)
            .Must(value => JsonValues.TryGetString(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => Trimmed(value).Length <= Transaction.NoteMaxLength).WithErrorCode(NoteTooLong)
            .Must(value => JsonValues.TryGetOptionalText(value, out _)).WithErrorCode(InvalidValue)
            .When(input => !JsonValues.IsMissing(input.Note));

        RuleFor(input => input.CategoryId)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetGuid(value, out _)).WithErrorCode(TransactionReferenceRules.UnknownCategory);

        RuleFor(input => input.AccountId)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetGuid(value, out _)).WithErrorCode(TransactionReferenceRules.UnknownAccount);

        RuleFor(input => input.Amount)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetSafeInteger(value, out var amount) && amount <= MinorUnits.MaxAmount).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetSafeInteger(value, out var amount) && amount > 0).WithErrorCode(PositiveAmount);

        RuleFor(input => input.Currency)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => CurrencyWire.TryParse(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(input => input.Rate)
            .Must(value => ManualRate.TryParse(value, out _)).WithErrorCode(InvalidValue)
            .When(input => !JsonValues.IsMissing(input.Rate));
    }

    private static string Trimmed(JsonElement? value) =>
        JsonValues.TryGetString(value, out var text) ? text.Trim() : string.Empty;
}
