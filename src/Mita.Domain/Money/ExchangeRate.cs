namespace Mita.Domain.Money;

public sealed class ExchangeRate(Currency currency, DateOnly date, decimal plnValue, DateTimeOffset fetchedAt)
{
    public Currency Currency { get; private set; } = currency;

    public DateOnly Date { get; private set; } = date;

    public decimal PlnValue { get; set; } = plnValue;

    public DateTimeOffset FetchedAt { get; set; } = fetchedAt;

    public const int MaxDecimals = 12;

    public const int MaxAgeDays = 30;

    public static decimal Round(decimal rate)
    {
        var decimals = 6;
        for (var scaled = Math.Abs(rate); scaled != 0 && scaled < 0.1m && decimals < MaxDecimals; scaled *= 10)
        {
            decimals++;
        }

        return Math.Round(rate, decimals, MidpointRounding.AwayFromZero);
    }

    public static decimal Cross(decimal fromPln, decimal toPln) => Round(fromPln / toPln);
}
