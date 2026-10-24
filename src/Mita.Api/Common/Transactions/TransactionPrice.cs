using Mita.Api.Common.Rates;
using Mita.Domain.Money;

namespace Mita.Api.Common.Transactions;

public sealed record TransactionPrice(decimal Rate, long AccountAmount, long BaseAmount)
{
    public static TransactionPrice? Compute(
        long amount,
        Currency currency,
        Currency accountCurrency,
        Currency baseCurrency,
        decimal? manualRate,
        IReadOnlyDictionary<Currency, decimal> plnValues)
    {
        var rate = currency == accountCurrency
            ? 1m
            : manualRate is { } manual
                ? ExchangeRate.Round(manual)
                : RateTable.Rate(plnValues, currency, accountCurrency);
        if (rate is not { } accountRate)
        {
            return null;
        }

        var accountAmount = MinorUnits.Convert(amount, accountRate, currency, accountCurrency);
        return PriceInBase(amount, currency, accountAmount, accountCurrency, baseCurrency, plnValues) is { } baseAmount
            ? new TransactionPrice(accountRate, accountAmount, baseAmount)
            : null;
    }

    public static long? PriceInBase(
        long amount,
        Currency currency,
        long accountAmount,
        Currency accountCurrency,
        Currency baseCurrency,
        IReadOnlyDictionary<Currency, decimal> plnValues)
    {
        if (currency == baseCurrency)
        {
            return amount;
        }

        return RateTable.Rate(plnValues, accountCurrency, baseCurrency) is { } baseRate
            ? MinorUnits.Convert(accountAmount, baseRate, accountCurrency, baseCurrency)
            : null;
    }
}
