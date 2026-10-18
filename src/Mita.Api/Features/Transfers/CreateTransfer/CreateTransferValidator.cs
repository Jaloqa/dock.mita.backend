using System.Text.Json;
using FluentValidation;
using Mita.Api.Common.Wire;
using Mita.Domain.Money;

namespace Mita.Api.Features.Transfers.CreateTransfer;

public sealed class CreateTransferValidator : AbstractValidator<CreateTransferCommand>
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string DateTooFar = "validation.dateTooFar";
    private const string UnknownAccount = "validation.unknownAccount";
    private const string PositiveAmount = "validation.positiveAmount";
    private const double MaxSafeInteger = 9_007_199_254_740_991;

    public CreateTransferValidator(TimeProvider time)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.Date)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => CreateTransferFields.TryGetDate(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => CreateTransferFields.TryGetDate(value, out var date) && date <= LatestDate(time))
            .WithErrorCode(DateTooFar);

        RuleFor(command => command.FromAccountId)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => CreateTransferFields.TryGetAccountId(value, out _)).WithErrorCode(UnknownAccount);

        RuleFor(command => command.ToAccountId)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => CreateTransferFields.TryGetAccountId(value, out _)).WithErrorCode(UnknownAccount);

        RuleFor(command => command.FromAmount)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => IsSafeInteger(value, out var amount) && amount <= MinorUnits.MaxAmount).WithErrorCode(InvalidValue)
            .Must(value => IsSafeInteger(value, out var amount) && amount > 0).WithErrorCode(PositiveAmount);

        RuleFor(command => command.Rate)
            .Must(value => CreateTransferFields.TryGetRate(value, out _)).WithErrorCode(InvalidValue)
            .When(command => !JsonValues.IsMissing(command.Rate));

        RuleFor(command => command.Note)
            .Must(value => CreateTransferFields.TryGetNote(value, out _)).WithErrorCode(InvalidValue);
    }

    private static DateOnly LatestDate(TimeProvider time) =>
        DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddYears(1);

    private static bool IsSafeInteger(JsonElement? element, out double value)
    {
        value = 0;
        return element is { ValueKind: JsonValueKind.Number } number
            && number.TryGetDouble(out value)
            && value == Math.Floor(value)
            && Math.Abs(value) <= MaxSafeInteger;
    }
}
