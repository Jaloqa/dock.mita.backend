using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Rates;
using Mita.Domain.Budgets;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Categories.DeleteCategory;

public static class BudgetReassignment
{
    public static async Task<bool> ReassignAsync(
        AppDbContext db,
        Guid userId,
        Guid fromCategoryId,
        Guid otherId,
        Currency baseCurrency,
        MonthStart monthStart,
        CancellationToken ct)
    {
        var budgets = await db.Budgets
            .FromSql($"""
                SELECT * FROM budgets
                WHERE "UserId" = {userId} AND ("CategoryId" = {fromCategoryId} OR "CategoryId" = {otherId})
                ORDER BY "Id"
                FOR UPDATE
                """)
            .ToListAsync(ct);
        var targets = budgets
            .Where(budget => budget.CategoryId == otherId)
            .ToDictionary(budget => budget.Period);

        foreach (var budget in budgets.Where(budget => budget.CategoryId == fromCategoryId))
        {
            if (!targets.TryGetValue(budget.Period, out var target))
            {
                budget.MoveTo(otherId);
                continue;
            }

            var from = FinancialPeriod.ByKey(budget.Period, monthStart).From;
            var targetLimit = await LimitInAsync(db, target, baseCurrency, from, ct);
            var movedLimit = await LimitInAsync(db, budget, baseCurrency, from, ct);
            if (targetLimit is null || movedLimit is null)
            {
                return false;
            }

            target.Absorb(targetLimit.Value + movedLimit.Value, baseCurrency);
            db.Budgets.Remove(budget);
        }

        return true;
    }

    private static async Task<long?> LimitInAsync(
        AppDbContext db,
        Budget budget,
        Currency baseCurrency,
        DateOnly date,
        CancellationToken ct)
    {
        if (budget.Currency == baseCurrency)
        {
            return budget.Limit;
        }

        var rate = RateTable.Rate(await db.PlnValuesAsync(date, ct), budget.Currency, baseCurrency);
        return rate is { } found ? Math.Max(1, MinorUnits.Convert(budget.Limit, found)) : null;
    }
}
