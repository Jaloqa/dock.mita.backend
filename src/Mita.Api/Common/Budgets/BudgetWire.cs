using Mita.Domain.Budgets;

namespace Mita.Api.Common.Budgets;

public static class BudgetWire
{
    public static string ToWire(BudgetStatus status) => status switch
    {
        BudgetStatus.OnTrack => "on_track",
        BudgetStatus.AlmostSpent => "almost_spent",
        BudgetStatus.OverBudget => "over_budget",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
