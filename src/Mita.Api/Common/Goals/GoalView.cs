using Mita.Api.Common.Money;
using Mita.Domain.Goals;
using Mita.Domain.Money;
using Mita.Domain.Periods;

namespace Mita.Api.Common.Goals;

public sealed record GoalSummary(long Saved, long Monthly, int Active);

public static class GoalView
{
    public const int SavingWindowMonths = 3;

    public static int MonthsUntil(PeriodKey deadline, DateOnly today) =>
        deadline.MonthIndex - PeriodKey.Of(today).MonthIndex;

    public static GoalStatus Status(long saved, long target, PeriodKey? deadline, DateOnly today)
    {
        if (saved >= target)
        {
            return GoalStatus.Reached;
        }

        return deadline is { } key && MonthsUntil(key, today) < 0 ? GoalStatus.Overdue : GoalStatus.Active;
    }

    public static long? Monthly(long remaining, PeriodKey? deadline, DateOnly today)
    {
        if (deadline is not { } key || remaining <= 0)
        {
            return null;
        }

        var months = MonthsUntil(key, today);
        if (months < 0)
        {
            return null;
        }

        var divisor = Math.Max(months, 1);
        return (remaining + divisor - 1) / divisor;
    }

    public static DateOnly SavingWindowStart(DateOnly today) => today.AddMonths(-SavingWindowMonths);

    public static GoalDto From(Goal goal, decimal rateToBase, Currency baseCurrency, DateOnly today)
    {
        var target = MinorUnits.Convert(goal.Target, rateToBase);
        var saved = MinorUnits.Convert(goal.Saved, rateToBase);
        var remaining = Math.Max(target - saved, 0);
        return new GoalDto(
            goal.Id,
            goal.Name,
            target,
            saved,
            remaining,
            Percent(saved, target),
            Monthly(remaining, goal.Deadline, today),
            GoalWire.ToWire(Status(saved, target, goal.Deadline, today)),
            goal.Deadline?.ToString(),
            goal.AccountId,
            CurrencyWire.ToWire(baseCurrency));
    }

    public static GoalSummary Summarize(IReadOnlyList<GoalDto> goals, long monthly) =>
        new(
            goals.Sum(goal => goal.Saved),
            monthly,
            goals.Count(goal => goal.Status != GoalWire.ToWire(GoalStatus.Reached)));

    private static int Percent(long saved, long target) =>
        target > 0 ? (int)Math.Min(Math.Max(saved, 0) * 100m / target, int.MaxValue) : 0;
}
