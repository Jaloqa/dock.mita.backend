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
        var start = await StartDateAsync(today, ct);
        if (start > today)
        {
            return 0;
        }

        var fetched = await source.FetchAsync(start, today, ct);
        var inserted = 0;
        foreach (var rate in fetched.Where(IsPlausible))
        {
            inserted += await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO exchange_rates ("Currency", "Date", "PlnValue", "FetchedAt")
                VALUES ({rate.Currency.ToString()}, {rate.Date}, {ExchangeRate.Round(rate.Value)}, {now})
                ON CONFLICT DO NOTHING
                """,
                ct);
        }

        if (inserted > 0)
        {
            await InvalidateCacheAsync(ct);
        }

        return inserted;
    }

    private static bool IsPlausible(PlnRate rate) =>
        ExchangeRate.Quoted.Contains(rate.Currency)
        && ExchangeRate.Round(rate.Value) is > 0 and <= MaxPlnValue;

    private async Task<DateOnly> StartDateAsync(DateOnly today, CancellationToken ct)
    {
        var latestPerCurrency = await db.ExchangeRates
            .AsNoTracking()
            .GroupBy(rate => rate.Currency)
            .Select(group => new { Currency = group.Key, Latest = group.Max(rate => rate.Date) })
            .ToListAsync(ct);

        return ExchangeRate.Quoted.All(currency => latestPerCurrency.Any(latest => latest.Currency == currency))
            ? latestPerCurrency.Min(latest => latest.Latest).AddDays(1)
            : today.AddDays(-options.Value.HistoryDays);
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
}
