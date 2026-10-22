using Mita.Api.Common.Rates;
using Mita.Domain.Money;

namespace Mita.Api.Common.Debts;

public sealed record DebtPrice(decimal? Rate, long? AccountAmount)
{
    public static DebtPrice? Compute(
        long amount,
        Currency currency,
        Currency? accountCurrency,
        decimal? manualRate,
        IReadOnlyDictionary<Currency, decimal> plnValues)
    {
        if (accountCurrency is not { } target)
        {
            return new DebtPrice(null, null);
        }

        var rate = currency == target
            ? 1m
            : manualRate is { } manual
                ? ExchangeRate.Round(manual)
                : RateTable.Rate(plnValues, currency, target);

        return rate is { } found
            ? new DebtPrice(found, MinorUnits.Convert(amount, found, currency, target))
            : null;
    }
}
