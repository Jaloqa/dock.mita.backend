using Mita.Api.Common.Wire;
using Mita.Domain.Money;
using Mita.Domain.Periods;

namespace Mita.Api.Common.Budgets;

public sealed record ParsedBudget(Guid CategoryId, PeriodKey Period, long Limit);

public static class BudgetInputParser
{
    public static ParsedBudget Parse(IBudgetInput input)
    {
        if (!JsonValues.TryGetGuid(input.CategoryId, out var categoryId)
            || !JsonValues.TryGetPeriodKey(input.Period, out var period)
            || !JsonValues.TryGetMinorAmount(input.Limit, out var limit))
        {
            throw new InvalidOperationException("The budget input reached the handler without passing validation.");
        }

        return new ParsedBudget(categoryId, period, limit);
    }
}
