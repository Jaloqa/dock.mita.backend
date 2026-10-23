using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Tags;
using Mita.Api.Common.Wire;
using Mita.Domain.Groups;
using Mita.Domain.Money;

namespace Mita.Api.Common.Groups;

public sealed record ParsedExpense(
    Guid PayerId,
    long Amount,
    Currency Currency,
    DateOnly Date,
    string CategoryId,
    IReadOnlyList<string> Tags,
    string? Description,
    decimal? ManualRate,
    ParsedSplit? Split);

public static class ExpenseInputParser
{
    public static ParsedExpense Parse(IExpenseInput input)
    {
        if (!JsonValues.TryGetGuid(input.PayerId, out var payerId)
            || !JsonValues.TryGetMinorAmount(input.Amount, out var amount)
            || !CurrencyWire.TryParse(input.Currency, out var currency)
            || !JsonValues.TryGetIsoDate(input.Date, out var date)
            || !JsonValues.TryGetOptionalText(input.CategoryId, out var categoryId)
            || !JsonValues.TryGetOptionalText(input.Description, out var description))
        {
            throw new InvalidOperationException("The expense input reached the handler without passing validation.");
        }

        decimal? manualRate = ManualRate.TryParse(input.Rate, out var rate) ? rate : null;
        return new ParsedExpense(
            payerId,
            amount,
            currency,
            date,
            categoryId ?? StandardGroupCategories.Default,
            TagNames.Parse(input.Tags),
            description,
            manualRate,
            GroupSplitInput.Parse(input.Split));
    }
}
