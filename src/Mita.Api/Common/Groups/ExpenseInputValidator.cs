using FluentValidation;
using FluentValidation.Results;
using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Tags;
using Mita.Api.Common.Time;
using Mita.Api.Common.Wire;
using Mita.Domain.Groups;
using Mita.Domain.Money;

namespace Mita.Api.Common.Groups;

public abstract class ExpenseInputValidator<T> : AbstractValidator<T>
    where T : IExpenseInput
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string PositiveAmount = "validation.positiveAmount";
    private const string DateTooFar = "validation.dateTooFar";
    private const string TitleTooLong = "validation.titleTooLong";

    protected ExpenseInputValidator(TimeProvider time)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(input => input.PayerId)
            .Must(value => !JsonValues.IsMissing(value)).WithErrorCode(Required)
            .Must(value => JsonValues.TryGetGuid(value, out _)).WithErrorCode(GroupSplitInput.UnknownMember);

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

        RuleFor(input => input.CategoryId)
            .Must(value => JsonValues.TryGetString(value, out var text)
                && (StandardGroupCategories.IsStandard(text) || Guid.TryParse(text, out _)))
            .WithErrorCode(GroupExpenseWriter.UnknownCategory)
            .When(input => !JsonValues.IsMissing(input.CategoryId));

        RuleFor(input => input.Tags).Custom((value, context) =>
        {
            if (TagNames.Error(value) is { } code)
            {
                context.AddFailure(new ValidationFailure(nameof(IExpenseInput.Tags), code) { ErrorCode = code });
            }
        });

        RuleFor(input => input.Description)
            .Must(value => JsonValues.TryGetOptionalText(value, out _)).WithErrorCode(InvalidValue)
            .Must(value => JsonValues.TryGetOptionalText(value, out var text)
                && (text is null || text.Length <= GroupExpense.DescriptionMaxLength))
            .WithErrorCode(TitleTooLong)
            .When(input => !JsonValues.IsMissing(input.Description));

        RuleFor(input => input.Rate)
            .Must(value => ManualRate.TryParse(value, out _)).WithErrorCode(InvalidValue)
            .When(input => !JsonValues.IsMissing(input.Rate));

        RuleFor(input => input.Split).Custom((value, context) =>
        {
            if (GroupSplitInput.Error(value, context.InstanceToValidate.Amount) is { } code)
            {
                context.AddFailure(new ValidationFailure(nameof(IExpenseInput.Split), code) { ErrorCode = code });
            }
        });
    }
}
