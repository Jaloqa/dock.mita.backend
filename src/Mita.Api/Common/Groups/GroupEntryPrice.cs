using Mita.Api.Common.Rates;
using Mita.Domain.Money;

namespace Mita.Api.Common.Groups;

public sealed record GroupEntryPrice(decimal Rate, bool RateManual, long GroupAmount)
{
    public static GroupEntryPrice? Compute(
        long amount,
        Currency currency,
        Currency groupCurrency,
        decimal? manualRate,
        IReadOnlyDictionary<Currency, decimal> plnValues)
    {
        if (currency == groupCurrency)
        {
            return new GroupEntryPrice(1m, false, amount);
        }

        if (manualRate is { } manual)
        {
            return Priced(ExchangeRate.Round(manual), true);
        }

        return RateTable.Rate(plnValues, currency, groupCurrency) is { } stored ? Priced(stored, false) : null;

        GroupEntryPrice Priced(decimal rate, bool rateManual) =>
            new(rate, rateManual, MinorUnits.Convert(amount, rate, currency, groupCurrency));
    }
}
