using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Groups;
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
        var debts = await db.Debts.AsNoTracking()
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

        var groups = (await db.LinkedBalancesAsync(userId, ct))
            .Where(group => group.Balance != 0)
            .ToList();

        IReadOnlyDictionary<Currency, decimal>? plnValues =
            debts.Any(debt => debt.Currency != baseCurrency) || groups.Any(group => group.Currency != baseCurrency)
                ? await db.PlnValuesAsync(today, ct)
                : null;

        long? InBase(long amount, Currency currency)
        {
            var rate = currency == baseCurrency ? 1m : RateTable.Rate(plnValues!, currency, baseCurrency);
            return rate is { } found ? MinorUnits.Convert(amount, found, currency, baseCurrency) : null;
        }

        long owedToMe = 0;
        long owedByMe = 0;
        var open = 0;
        var overdue = 0;
        foreach (var debt in debts)
        {
            if (InBase(debt.Remaining, debt.Currency) is not { } converted)
            {
                return null;
            }

            if (debt.Direction == DebtDirection.Lent)
            {
                owedToMe += converted;
            }
            else
            {
                owedByMe += converted;
            }

            open += debt.Open;
            overdue += debt.Overdue;
        }

        foreach (var group in groups)
        {
            if (InBase(Math.Abs(group.Balance), group.Currency) is not { } converted)
            {
                return null;
            }

            if (group.Balance > 0)
            {
                owedToMe += converted;
            }
            else
            {
                owedByMe += converted;
            }
        }

        return new DebtSummaryDto(owedToMe, owedByMe, open, overdue, groups.Count);
    }
}
