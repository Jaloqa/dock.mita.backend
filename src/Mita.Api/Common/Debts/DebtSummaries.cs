using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Rates;
using Mita.Domain.Debts;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Debts;

public static class DebtSummaries
{
    public static async Task<DebtSummaryDto?> SummarizeAsync(
        this AppDbContext db,
        Guid userId,
        Currency baseCurrency,
        DateOnly today,
        CancellationToken ct)
    {
        var groups = await db.Debts.AsNoTracking()
            .Where(debt => debt.UserId == userId && debt.Repaid < debt.Amount)
            .GroupBy(debt => new { debt.Direction, debt.Currency })
            .Select(group => new
            {
                group.Key.Direction,
                group.Key.Currency,
                Remaining = group.Sum(debt => debt.Amount - debt.Repaid),
                Open = group.Count(),
                Overdue = group.Count(debt => debt.DueDate != null && debt.DueDate < today)
            })
            .ToListAsync(ct);

        IReadOnlyDictionary<Currency, decimal>? plnValues = groups.Any(group => group.Currency != baseCurrency)
            ? await db.PlnValuesAsync(today, ct)
            : null;

        long owedToMe = 0;
        long owedByMe = 0;
        var open = 0;
        var overdue = 0;
        foreach (var group in groups)
        {
            var rate = group.Currency == baseCurrency ? 1m : RateTable.Rate(plnValues!, group.Currency, baseCurrency);
            if (rate is not { } found)
            {
                return null;
            }

            var converted = MinorUnits.Convert(group.Remaining, found, group.Currency, baseCurrency);
            if (group.Direction == DebtDirection.Lent)
            {
                owedToMe += converted;
            }
            else
            {
                owedByMe += converted;
            }

            open += group.Open;
            overdue += group.Overdue;
        }

        return new DebtSummaryDto(owedToMe, owedByMe, open, overdue);
    }
}
