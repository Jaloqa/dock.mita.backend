using Microsoft.EntityFrameworkCore;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Rates;

public static class RateTable
{
    public static async Task<IReadOnlyDictionary<Currency, decimal>> PlnValuesAsync(
        this AppDbContext db,
        DateOnly date,
        CancellationToken ct) =>
        ToValues(await LoadAsync(db, date, ct), null);

    public static async Task<IReadOnlyDictionary<Currency, decimal>> FreshPlnValuesAsync(
        this AppDbContext db,
        DateOnly date,
        DateOnly today,
        CancellationToken ct)
    {
        var oldestAllowed = (date < today ? date : today).AddDays(-ExchangeRate.MaxAgeDays);
        return ToValues(await LoadAsync(db, date, ct), oldestAllowed);
    }

    public static async Task<decimal?> ResolveAsync(
        this AppDbContext db,
        Currency from,
        Currency to,
        decimal? manualRate,
        DateOnly date,
        DateOnly today,
        CancellationToken ct)
    {
        if (from == to)
        {
            return 1m;
        }

        if (manualRate is { } manual)
        {
            return ExchangeRate.Round(manual);
        }

        return Rate(await db.FreshPlnValuesAsync(date, today, ct), from, to);
    }

    public static decimal? Rate(IReadOnlyDictionary<Currency, decimal> plnValues, Currency from, Currency to)
    {
        if (from == to)
        {
            return 1m;
        }

        return plnValues.TryGetValue(from, out var fromPln) && plnValues.TryGetValue(to, out var toPln)
            ? ExchangeRate.Cross(fromPln, toPln)
            : null;
    }

    private static Dictionary<Currency, decimal> ToValues(List<RateRow> rows, DateOnly? oldestAllowed)
    {
        var values = new Dictionary<Currency, decimal> { [Currency.PLN] = 1m };
        foreach (var row in rows)
        {
            if (oldestAllowed is { } limit && !row.IsEarliestFallback && row.Date < limit)
            {
                continue;
            }

            if (Currencies.TryParse(row.Currency, out var currency) && currency != Currency.PLN)
            {
                values[currency] = row.PlnValue;
            }
        }

        return values;
    }

    private static Task<List<RateRow>> LoadAsync(AppDbContext db, DateOnly date, CancellationToken ct) =>
        db.Database
            .SqlQuery<RateRow>($"""
                WITH latest AS (
                    SELECT DISTINCT ON ("Currency") "Currency", "Date", "PlnValue"
                    FROM exchange_rates
                    WHERE "Date" <= {date}
                    ORDER BY "Currency", "Date" DESC
                ),
                earliest AS (
                    SELECT DISTINCT ON (rate."Currency") rate."Currency", rate."Date", rate."PlnValue"
                    FROM exchange_rates AS rate
                    WHERE NOT EXISTS (SELECT 1 FROM latest WHERE latest."Currency" = rate."Currency")
                    ORDER BY rate."Currency", rate."Date"
                )
                SELECT "Currency", "Date", "PlnValue", FALSE AS "IsEarliestFallback" FROM latest
                UNION ALL
                SELECT "Currency", "Date", "PlnValue", TRUE AS "IsEarliestFallback" FROM earliest
                """)
            .ToListAsync(ct);

    private sealed class RateRow
    {
        public string Currency { get; init; } = string.Empty;

        public DateOnly Date { get; init; }

        public decimal PlnValue { get; init; }

        public bool IsEarliestFallback { get; init; }
    }
}
