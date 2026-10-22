using Mita.Api.Common.Rates;
using Mita.Domain.Money;

namespace Mita.Api.Features.Debts.AddRepayment;

public sealed record RepaymentPrice(decimal Rate, long DebtAmount, long? AccountAmount)
{
    public static RepaymentPrice? Compute(
        long amount,
        Currency currency,
        Currency debtCurrency,
        Currency? accountCurrency,
        decimal? manualRate,
        IReadOnlyDictionary<Currency, decimal> plnValues)
    {
        var rate = currency == debtCurrency
            ? 1m
            : manualRate is { } manual
                ? ExchangeRate.Round(manual)
                : RateTable.Rate(plnValues, currency, debtCurrency);
        if (rate is not { } debtRate)
        {
            return null;
        }

        var debtAmount = MinorUnits.Convert(amount, debtRate, currency, debtCurrency);
        if (accountCurrency is not { } target)
        {
            return new RepaymentPrice(debtRate, debtAmount, null);
        }

        if (target == currency)
        {
            return new RepaymentPrice(debtRate, debtAmount, amount);
        }

        if (target == debtCurrency)
        {
            return new RepaymentPrice(debtRate, debtAmount, debtAmount);
        }

        return RateTable.Rate(plnValues, currency, target) is { } accountRate
            ? new RepaymentPrice(debtRate, debtAmount, MinorUnits.Convert(amount, accountRate, currency, target))
            : null;
    }
}
