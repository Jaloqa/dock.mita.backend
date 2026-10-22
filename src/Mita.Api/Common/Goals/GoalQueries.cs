using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Rates;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Goals;

public static class GoalQueries
{
    public static async Task<IReadOnlyList<GoalDto>?> GoalsAsync(
        this AppDbContext db,
        Guid userId,
        Currency baseCurrency,
        DateOnly today,
        CancellationToken ct)
    {
        var goals = await db.Goals.AsNoTracking()
            .Where(goal => goal.UserId == userId)
            .OrderBy(goal => goal.CreatedAt)
            .ThenBy(goal => goal.Id)
            .ToListAsync(ct);

        IReadOnlyDictionary<Currency, decimal>? plnValues = goals.Any(goal => goal.Currency != baseCurrency)
            ? await db.PlnValuesAsync(today, ct)
            : null;

        var items = new List<GoalDto>(goals.Count);
        foreach (var goal in goals)
        {
            var rate = goal.Currency == baseCurrency ? 1m : RateTable.Rate(plnValues!, goal.Currency, baseCurrency);
            if (rate is not { } found)
            {
                return null;
            }

            items.Add(GoalView.From(goal, found, baseCurrency, today));
        }

        return items;
    }

    public static async Task<long?> MonthlySavingAsync(
        this AppDbContext db,
        Guid userId,
        Currency baseCurrency,
        DateOnly today,
        CancellationToken ct)
    {
        var since = GoalView.SavingWindowStart(today);
        var baseName = baseCurrency.ToString();
        var sums = await db.Database
            .SqlQuery<decimal?>($"""
                SELECT CASE WHEN bool_or(amount IS NULL) THEN NULL ELSE COALESCE(sum(amount), 0) END AS "Value"
                FROM (
                    SELECT CASE WHEN "BaseCurrency" = {baseName} THEN "BaseAmount"::numeric
                        ELSE round("BaseAmount" * round_rate(pln_value("BaseCurrency", "Date") / pln_value({baseName}, "Date"))) END AS amount
                    FROM goal_contributions
                    WHERE "UserId" = {userId} AND "Date" > {since} AND "Date" <= {today}
                ) AS contributions
                """)
            .ToListAsync(ct);

        return sums.Single() is { } sum
            ? (long)Math.Round(sum / GoalView.SavingWindowMonths, MidpointRounding.AwayFromZero)
            : null;
    }
}
