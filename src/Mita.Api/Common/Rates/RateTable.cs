using Microsoft.EntityFrameworkCore;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Rates;

public static class RateTable
{
    public static async Task<IReadOnlyDictionary<Currency, decimal>> PlnValuesAsync(
        this AppDbContext db,
        DateOnly date,
        CancellationToken ct)
    {
        var values = new Dictionary<Currency, decimal> { [Currency.PLN] = 1m };
        foreach (var currency in ExchangeRate.Quoted)
        {
            var rates = db.ExchangeRates.AsNoTracking().Where(rate => rate.Currency == currency);
            var latestBefore = await rates
                .Where(rate => rate.Date <= date)
                .OrderByDescending(rate => rate.Date)
                .Select(rate => (decimal?)rate.PlnValue)
                .FirstOrDefaultAsync(ct);
            var value = latestBefore ?? await rates
                .OrderBy(rate => rate.Date)
                .Select(rate => (decimal?)rate.PlnValue)
                .FirstOrDefaultAsync(ct);

            if (value is { } found)
            {
                values[currency] = found;
            }
        }

        return values;
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
}
