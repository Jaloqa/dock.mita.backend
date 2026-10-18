using System.Text.Json;
using FluentValidation;
using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;
using Mita.Domain.Accounts;

namespace Mita.Api.Common.Accounts;

public abstract class AccountInputValidator<T> : AbstractValidator<T>
    where T : IAccountInput
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string NameTooLong = "validation.nameTooLong";
    private const string CardLast4 = "validation.cardLast4";
    private const int CardLast4Length = 4;

    protected AccountInputValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(input => input.Name)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetString(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => Trimmed(value).Length <= Account.NameMaxLength).WithErrorCode(NameTooLong)
            .Must(value => !Trimmed(value).Any(char.IsControl)).WithErrorCode(InvalidValue);

        RuleFor(input => input.Type)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => AccountWire.TryParse(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(input => input.Currency)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => CurrencyWire.TryParse(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(input => input.OpeningBalance)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetMinorAmount(value, out _)).WithErrorCode(InvalidValue);

        RuleFor(input => input.CardLast4)
            .Must(IsFourDigits).WithErrorCode(CardLast4)
            .When(input => !JsonValues.IsMissing(input.CardLast4));

        RuleFor(input => input.Archived)
            .Must(value => JsonValues.TryGetBoolean(value, out _)).WithErrorCode(InvalidValue)
            .When(input => input.Archived is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) });
    }

    private static string Trimmed(JsonElement? value) =>
        JsonValues.TryGetString(value, out var text) ? text.Trim() : string.Empty;

    private static bool IsFourDigits(JsonElement? value) =>
        JsonValues.TryGetString(value, out var text)
        && text.Length == CardLast4Length
        && text.All(char.IsAsciiDigit);
}
