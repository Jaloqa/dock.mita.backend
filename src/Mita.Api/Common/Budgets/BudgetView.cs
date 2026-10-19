using Mita.Domain.Budgets;

namespace Mita.Api.Common.Budgets;

public sealed record BudgetSummary(long Planned, long Spent, long Left, int DaysLeft);

public static class BudgetView
{
    public static BudgetStatus Status(long spent, long limit)
    {
        if (spent > limit)
        {
            return BudgetStatus.OverBudget;
        }

        return spent * 100 >= limit * 85 ? BudgetStatus.AlmostSpent : BudgetStatus.OnTrack;
    }

    public static long? PerDay(long remaining, int daysLeft) =>
        remaining > 0 && daysLeft > 0 ? remaining / daysLeft : null;

    public static BudgetDto From(Budget budget, long limitInBase, long spent, int daysLeft)
    {
        var remaining = limitInBase - spent;
        return new BudgetDto(
            budget.Id,
            budget.CategoryId,
            budget.Period.ToString(),
            limitInBase,
            spent,
            remaining,
            BudgetWire.ToWire(Status(spent, limitInBase)),
            PerDay(remaining, daysLeft));
    }

    public static BudgetSummary Summarize(IReadOnlyList<BudgetDto> budgets, int daysLeft)
    {
        var planned = budgets.Sum(budget => budget.Limit);
        var spent = budgets.Sum(budget => budget.Spent);
        return new BudgetSummary(planned, spent, planned - spent, daysLeft);
    }
}
