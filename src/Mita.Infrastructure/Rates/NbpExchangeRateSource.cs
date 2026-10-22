using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Mita.Domain.Money;

namespace Mita.Infrastructure.Rates;

public sealed partial class NbpExchangeRateSource(HttpClient http, ILogger<NbpExchangeRateSource> logger)
    : IExchangeRateSource
{
    private const int MaxWindowDays = 93;
    private const string UnitOfAccount = "XDR";

    public async Task<IReadOnlyList<PlnRate>> FetchAsync(
        NbpTable table,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var rates = new List<PlnRate>();
        var unsupported = new HashSet<string>(StringComparer.Ordinal);
        for (var start = from; start <= to; start = start.AddDays(MaxWindowDays))
        {
            var end = start.AddDays(MaxWindowDays - 1) < to ? start.AddDays(MaxWindowDays - 1) : to;
            await FetchWindowAsync(table, start, end, rates, unsupported, ct);
        }

        return rates;
    }

    private async Task FetchWindowAsync(
        NbpTable table,
        DateOnly from,
        DateOnly to,
        List<PlnRate> rates,
        HashSet<string> unsupported,
        CancellationToken ct)
    {
        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"exchangerates/tables/{table}/{from:yyyy-MM-dd}/{to:yyyy-MM-dd}/?format=json");
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
        var days = await response.Content.ReadFromJsonAsync<List<NbpDay>>(ct) ?? [];
        foreach (var day in days)
        {
            foreach (var rate in day.Rates ?? [])
            {
                if (rate.Code is null || rate.Code == UnitOfAccount)
                {
                    continue;
                }

                if (Currencies.TryParse(rate.Code, out var currency) && currency != Currency.PLN)
                {
                    rates.Add(new PlnRate(currency, day.EffectiveDate, rate.Mid));
                }
                else if (unsupported.Add(rate.Code))
                {
                    LogUnsupportedCurrency(logger, table, rate.Code);
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "NBP table {Table} has rates for unsupported currency {Code}")]
    private static partial void LogUnsupportedCurrency(ILogger logger, NbpTable table, string code);

    private sealed record NbpDay(DateOnly EffectiveDate, List<NbpRate>? Rates);

    private sealed record NbpRate(string? Code, decimal Mid);
}
