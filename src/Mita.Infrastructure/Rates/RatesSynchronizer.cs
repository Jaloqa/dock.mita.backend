using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mita.Domain.Money;
using Mita.Infrastructure.Caching;
using Mita.Infrastructure.Persistence;

namespace Mita.Infrastructure.Rates;

public sealed partial class RatesSynchronizer(
    AppDbContext db,
    IExchangeRateSource source,
    HybridCache cache,
    TimeProvider time,
    IOptions<RatesOptions> options,
    ILogger<RatesSynchronizer> logger)
{
    private const decimal MaxPlnValue = 1_000_000m;

    public async Task<int> SyncAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var latestPerCurrency = await db.ExchangeRates
            .AsNoTracking()
            .GroupBy(rate => rate.Currency)
            .Select(group => new { Currency = group.Key, Latest = group.Max(rate => rate.Date) })
            .ToDictionaryAsync(entry => entry.Currency, entry => entry.Latest, ct);

        var inserted = 0;
        var failures = new List<Exception>();
        foreach (var table in NbpTables.All)
        {
            try
            {
                inserted += await SyncTableAsync(table, today, now, latestPerCurrency, ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                LogTableSyncFailed(logger, table, exception);
                failures.Add(exception);
            }
        }

        if (failures.Count == NbpTables.All.Count)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (inserted > 0)
        {
            await InvalidateCacheAsync(ct);
        }

        return inserted;
    }

    private static bool IsPlausible(PlnRate rate) =>
        rate.Currency != Currency.PLN
        && ExchangeRate.Round(rate.Value) is > 0 and <= MaxPlnValue;

    private async Task<int> SyncTableAsync(
        NbpTable table,
        DateOnly today,
        DateTimeOffset now,
        Dictionary<Currency, DateOnly> latestPerCurrency,
        CancellationToken ct)
    {
        var start = StartDate(table, today, latestPerCurrency);
        if (start > today)
        {
            return 0;
        }

        var fetched = await source.FetchAsync(table, start, today, ct);
        var plausible = fetched.Where(IsPlausible).ToList();
        if (plausible.Count == 0)
        {
            return 0;
        }

        var currencies = plausible.Select(rate => rate.Currency.ToString()).ToArray();
        var dates = plausible.Select(rate => rate.Date).ToArray();
        var values = plausible.Select(rate => ExchangeRate.Round(rate.Value)).ToArray();
        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO exchange_rates ("Currency", "Date", "PlnValue", "FetchedAt")
            SELECT rate.currency, rate.date, rate.value, {now}
            FROM unnest({currencies}::text[], {dates}::date[], {values}::numeric[]) AS rate(currency, date, value)
            ON CONFLICT DO NOTHING
            """,
            ct);
    }

    private DateOnly StartDate(NbpTable table, DateOnly today, Dictionary<Currency, DateOnly> latestPerCurrency)
    {
        var currencies = NbpTables.CurrenciesOf(table);
        var withoutRows = currencies.Where(currency => !latestPerCurrency.ContainsKey(currency)).ToList();
        if (withoutRows.Count == 0)
        {
            return currencies.Max(currency => latestPerCurrency[currency]).AddDays(1);
        }

        var from = today.AddDays(-options.Value.HistoryDays);
        var names = string.Join(", ", withoutRows);
        LogFullWindowFetch(logger, table, names, from);
        return from;
    }

    private async Task InvalidateCacheAsync(CancellationToken ct)
    {
        try
        {
            await cache.RemoveByTagAsync(GlobalCacheTags.Rates, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogInvalidationFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not invalidate the exchange rates cache after a sync")]
    private static partial void LogInvalidationFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exchange rates sync of NBP table {Table} failed")]
    private static partial void LogTableSyncFailed(ILogger logger, NbpTable table, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "NBP table {Table} has no stored rates for {Currencies}; fetching from {From}")]
    private static partial void LogFullWindowFetch(ILogger logger, NbpTable table, string currencies, DateOnly from);
}
