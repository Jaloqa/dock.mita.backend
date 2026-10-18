using System.Threading.Channels;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mita.Infrastructure.Authentication;

public sealed partial class SecurityStampEvictions(
    HybridCache cache,
    TimeProvider time,
    ILogger<SecurityStampEvictions> logger) : BackgroundService
{
    // A request that missed the cache before the stamp changed can still write the old stamp back after the
    // first eviction; evicting again once such in-flight reads have finished removes that late write.
    public static readonly TimeSpan RepeatDelay = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan EvictionTimeout = TimeSpan.FromSeconds(2);

    private readonly Channel<PendingEviction> pending = Channel.CreateUnbounded<PendingEviction>(
        new UnboundedChannelOptions { SingleReader = true });

    public async Task EvictAsync(Guid userId)
    {
        await RemoveAsync(userId);
        pending.Writer.TryWrite(new PendingEviction(userId, time.GetUtcNow() + RepeatDelay));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await WaitForPendingAsync(stoppingToken))
        {
            while (pending.Reader.TryRead(out var eviction))
            {
                await DelayUntilAsync(eviction.DueAt, stoppingToken);
                await RemoveAsync(eviction.UserId);
            }
        }
    }

    private async Task<bool> WaitForPendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            return await pending.Reader.WaitToReadAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return pending.Reader.TryPeek(out _);
        }
    }

    private async Task DelayUntilAsync(DateTimeOffset dueAt, CancellationToken stoppingToken)
    {
        var wait = dueAt - time.GetUtcNow();
        if (wait <= TimeSpan.Zero || stoppingToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await Task.Delay(wait, time, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RemoveAsync(Guid userId)
    {
        using var timeout = new CancellationTokenSource(EvictionTimeout);
        try
        {
            await cache.RemoveAsync(SecurityStampCache.Key(userId), timeout.Token);
        }
        catch (Exception exception)
        {
            LogEvictionFailed(logger, userId, exception.GetType().Name);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not evict the cached security stamp of user {UserId} ({ErrorType})")]
    private static partial void LogEvictionFailed(ILogger logger, Guid userId, string errorType);

    private sealed record PendingEviction(Guid UserId, DateTimeOffset DueAt);
}
