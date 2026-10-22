using Mita.Domain.Money;
using Mita.Infrastructure.Rates;

namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed class FakeExchangeRateSource : IExchangeRateSource
{
    private readonly Lock gate = new();
    private readonly List<(DateOnly From, DateOnly To)> requests = [];

    public Func<Currency, DateOnly, decimal> ValueFor { get; set; } = DefaultValue;

    public Exception? Failure { get; set; }

    public IReadOnlyList<(DateOnly From, DateOnly To)> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    public static decimal DefaultValue(Currency currency, DateOnly date)
    {
        var basis = currency switch
        {
            Currency.EUR => 4.2m,
            Currency.USD => 3.9m,
            _ => 0.09m
        };
        return basis + (date.DayNumber % 100 * 0.0001m);
    }

    public Task<IReadOnlyList<PlnRate>> FetchAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        lock (gate)
        {
            requests.Add((from, to));
        }

        if (Failure is not null)
        {
            throw Failure;
        }

        var rates = new List<PlnRate>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            rates.AddRange(new[] { Currency.EUR, Currency.USD, Currency.UAH }.Select(currency => new PlnRate(currency, date, ValueFor(currency, date))));
        }

        return Task.FromResult<IReadOnlyList<PlnRate>>(rates);
    }
}
