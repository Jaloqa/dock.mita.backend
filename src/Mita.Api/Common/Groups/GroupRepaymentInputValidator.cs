using System.Text.Json;
using FluentValidation;
using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Time;
using Mita.Api.Common.Wire;
using Mita.Domain.Groups;
using Mita.Domain.Money;

namespace Mita.Api.Common.Groups;

public abstract class GroupRepaymentInputValidator<T> : AbstractValidator<T>
    where T : IGroupRepaymentInput
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string PositiveAmount = "validation.positiveAmount";
    private const string DateTooFar = "validation.dateTooFar";
    private const string SameMember = "validation.sameMember";
    private const string NoteTooLong = "validation.noteTooLong";

    protected GroupRepaymentInputValidator(TimeProvider time)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(input => input.FromMemberId)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetGuid(value, out _)).WithErrorCode(GroupSplitInput.UnknownMember);

        RuleFor(input => input.ToMemberId)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetGuid(value, out _)).WithErrorCode(GroupSplitInput.UnknownMember)
            .Must((input, value) => !IsSameMember(input.FromMemberId, value)).WithErrorCode(SameMember);

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

        RuleFor(input => input.Rate)
            .Must(value => ManualRate.TryParse(value, out _)).WithErrorCode(InvalidValue)
            .When(input => !JsonValues.IsMissing(input.Rate));

        RuleFor(input => input.Note)
            .Must(value => JsonValues.TryGetOptionalText(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetOptionalText(value, out var text)
                && (text is null || text.Length <= GroupRepayment.NoteMaxLength))
            .WithErrorCode(NoteTooLong)
            .When(input => !JsonValues.IsMissing(input.Note));
    }

    private static bool IsSameMember(JsonElement? from, JsonElement? to) =>
        JsonValues.TryGetGuid(from, out var fromId) && JsonValues.TryGetGuid(to, out var toId) && fromId == toId;
}
