using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mita.Infrastructure.Rates;

public sealed partial class RatesSyncService(
    IServiceScopeFactory scopes,
    IOptions<RatesOptions> options,
    TimeProvider time,
    ILogger<RatesSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.SyncEnabled)
        {
            return;
        }

        await Task.Yield();
        try
        {
            await SyncOnceAsync(stoppingToken);
            using var timer = new PeriodicTimer(options.Value.SyncInterval, time);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SyncOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task SyncOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var inserted = await scope.ServiceProvider.GetRequiredService<RatesSynchronizer>().SyncAsync(ct);
            LogSynced(logger, inserted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogSyncFailed(logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Exchange rates sync stored {Inserted} new rows")]
    private static partial void LogSynced(ILogger logger, int inserted);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Exchange rates sync failed, the next run will retry")]
    private static partial void LogSyncFailed(ILogger logger, Exception exception);
}
