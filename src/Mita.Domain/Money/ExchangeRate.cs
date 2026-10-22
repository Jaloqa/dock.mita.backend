namespace Mita.Domain.Money;

public sealed class ExchangeRate(Currency currency, DateOnly date, decimal plnValue, DateTimeOffset fetchedAt)
{
    public Currency Currency { get; private set; } = currency;

    public DateOnly Date { get; private set; } = date;

    public decimal PlnValue { get; set; } = plnValue;

    public DateTimeOffset FetchedAt { get; set; } = fetchedAt;

    public static decimal Round(decimal rate) => Math.Round(rate, 6, MidpointRounding.AwayFromZero);

    public static decimal Cross(decimal fromPln, decimal toPln) => Round(fromPln / toPln);
}
