using Mita.Domain.Money;

namespace Mita.Infrastructure.Rates;

public enum NbpTable
{
    A,
    B
}

public static class NbpTables
{
    private static readonly HashSet<Currency> TableA =
    [
        Currency.AUD, Currency.BRL, Currency.CAD, Currency.CHF, Currency.CLP, Currency.CNY, Currency.CZK,
        Currency.DKK, Currency.EUR, Currency.GBP, Currency.HKD, Currency.HUF, Currency.IDR, Currency.ILS,
        Currency.INR, Currency.ISK, Currency.JPY, Currency.KRW, Currency.MXN, Currency.MYR, Currency.NOK,
        Currency.NZD, Currency.PHP, Currency.RON, Currency.SEK, Currency.SGD, Currency.THB, Currency.TRY,
        Currency.UAH, Currency.USD, Currency.ZAR
    ];

    private static readonly IReadOnlyList<Currency> TableACurrencies =
        [.. Currencies.Quoted.Where(TableA.Contains)];

    private static readonly IReadOnlyList<Currency> TableBCurrencies =
        [.. Currencies.Quoted.Where(currency => !TableA.Contains(currency))];

    public static IReadOnlyList<NbpTable> All { get; } = [NbpTable.A, NbpTable.B];

    public static IReadOnlyList<Currency> CurrenciesOf(NbpTable table) =>
        table == NbpTable.A ? TableACurrencies : TableBCurrencies;

    public static NbpTable Of(Currency currency)
    {
        if (currency == Currency.PLN)
        {
            throw new ArgumentOutOfRangeException(nameof(currency), currency, "PLN is not quoted by NBP.");
        }

        return TableA.Contains(currency) ? NbpTable.A : NbpTable.B;
    }
}
