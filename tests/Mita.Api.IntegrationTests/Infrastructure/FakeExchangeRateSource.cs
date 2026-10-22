using Mita.Domain.Money;
using Mita.Infrastructure.Rates;

namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed class FakeExchangeRateSource : IExchangeRateSource
{
    private readonly Lock gate = new();
    private readonly List<(NbpTable Table, DateOnly From, DateOnly To)> requests = [];

    public Func<Currency, DateOnly, decimal> ValueFor { get; set; } = DefaultValue;

    public Exception? Failure { get; set; }

    public NbpTable? FailingTable { get; set; }

    public IReadOnlyList<(NbpTable Table, DateOnly From, DateOnly To)> Requests
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
            Currency.UAH => 0.09m,
            _ => 0.5m
        };
        return basis + (date.DayNumber % 100 * 0.0001m);
    }

    public Task<IReadOnlyList<PlnRate>> FetchAsync(NbpTable table, DateOnly from, DateOnly to, CancellationToken ct)
    {
        lock (gate)
        {
            requests.Add((table, from, to));
        }

        if (Failure is not null && (FailingTable is null || FailingTable == table))
        {
            throw Failure;
        }

        var currencies = NbpTables.CurrenciesOf(table);
        var rates = new List<PlnRate>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (table == NbpTable.B && date.DayOfWeek != DayOfWeek.Wednesday)
            {
                continue;
            }

            rates.AddRange(currencies.Select(currency => new PlnRate(currency, date, ValueFor(currency, date))));
        }

        return Task.FromResult<IReadOnlyList<PlnRate>>(rates);
    }
}
