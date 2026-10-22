using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Mita.Domain.Money;

namespace Mita.Infrastructure.Rates;

public sealed class NbpExchangeRateSource(HttpClient http) : IExchangeRateSource
{
    private const int MaxWindowDays = 93;

    private static readonly Dictionary<string, Currency> Quoted =
        SyncedCurrencies.All.ToDictionary(currency => currency.ToString(), StringComparer.Ordinal);

    public async Task<IReadOnlyList<PlnRate>> FetchAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rates = new List<PlnRate>();
        for (var start = from; start <= to; start = start.AddDays(MaxWindowDays))
        {
            var end = start.AddDays(MaxWindowDays - 1) < to ? start.AddDays(MaxWindowDays - 1) : to;
            rates.AddRange(await FetchWindowAsync(start, end, ct));
        }

        return rates;
    }

    private async Task<List<PlnRate>> FetchWindowAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"exchangerates/tables/A/{from:yyyy-MM-dd}/{to:yyyy-MM-dd}/?format=json");
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        var tables = await response.Content.ReadFromJsonAsync<List<NbpTable>>(ct) ?? [];
        return
        [
            .. tables
                .SelectMany(table => table.Rates ?? [], (table, rate) => (table, rate))
                .Where(entry => entry.rate.Code is not null && Quoted.ContainsKey(entry.rate.Code))
                .Select(entry => new PlnRate(Quoted[entry.rate.Code!], entry.table.EffectiveDate, entry.rate.Mid))
        ];
    }

    private sealed record NbpTable(DateOnly EffectiveDate, List<NbpRate>? Rates);

    private sealed record NbpRate(string? Code, decimal Mid);
}
