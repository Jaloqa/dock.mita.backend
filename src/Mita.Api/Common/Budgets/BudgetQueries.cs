using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Rates;
using Mita.Domain.Categories;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Budgets;

public static class BudgetQueries
{
    public static async Task<IReadOnlyList<BudgetDto>?> BudgetsInAsync(
        this AppDbContext db,
        Guid userId,
        FinancialPeriod period,
        Currency baseCurrency,
        DateOnly today,
        CancellationToken ct)
    {
        var key = period.Key;
        var budgets = await db.Budgets.AsNoTracking()
            .Where(budget => budget.UserId == userId && budget.Period == key)
            .OrderBy(budget => budget.CreatedAt)
            .ThenBy(budget => budget.Position)
            .ThenBy(budget => budget.Id)
            .ToListAsync(ct);
        if (budgets.Count == 0)
        {
            return [];
        }

        var categoryIds = budgets.Select(budget => budget.CategoryId).ToList();
        var from = period.From;
        var to = period.To;
        var spentByCategory = await db.Transactions.AsNoTracking()
            .Where(transaction => transaction.UserId == userId
                && transaction.Kind == CategoryKind.Expense
                && transaction.Date >= from
                && transaction.Date <= to
                && categoryIds.Contains(transaction.CategoryId))
            .GroupBy(transaction => transaction.CategoryId)
            .Select(group => new { CategoryId = group.Key, Spent = group.Sum(transaction => transaction.BaseAmount) })
            .ToDictionaryAsync(row => row.CategoryId, row => row.Spent, ct);

        IReadOnlyDictionary<Currency, decimal>? plnValues = budgets.Any(budget => budget.Currency != baseCurrency)
            ? await db.PlnValuesAsync(period.From, ct)
            : null;

        var daysLeft = period.DaysLeft(today);
        var items = new List<BudgetDto>(budgets.Count);
        foreach (var budget in budgets)
        {
            var limit = budget.Limit;
            if (budget.Currency != baseCurrency)
            {
                var rate = RateTable.Rate(plnValues!, budget.Currency, baseCurrency);
                if (rate is not { } found)
                {
                    return null;
                }

                limit = MinorUnits.Convert(budget.Limit, found, budget.Currency, baseCurrency);
            }

            items.Add(BudgetView.From(budget, limit, spentByCategory.GetValueOrDefault(budget.CategoryId), daysLeft));
        }

        return items;
    }
}
