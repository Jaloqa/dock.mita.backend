using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Mita.Infrastructure.Caching;
using Mita.Infrastructure.Persistence;

namespace Mita.Infrastructure.Authentication;

public sealed partial class SecurityStampCache(
    HybridCache cache,
    AppDbContext db,
    SecurityStampEvictions evictions,
    ILogger<SecurityStampCache> logger)
{
    // Every pod keeps its own L1 copy and only the pod that changed the stamp evicts it, so a short local
    // lifetime bounds how long other pods keep accepting a revoked token.
    public static readonly TimeSpan LocalExpiration = TimeSpan.FromSeconds(30);

    private static readonly HybridCacheEntryOptions EntryOptions = new()
    {
        Expiration = CacheDefaults.Expiration,
        LocalCacheExpiration = LocalExpiration
    };

    public static string Key(Guid userId) => $"stamp:{userId}";

    public async Task<string?> GetAsync(Guid userId, CancellationToken ct)
    {
        var lookup = new Lookup(this, userId);
        try
        {
            return await cache.GetOrCreateAsync(
                Key(userId),
                lookup,
                static (state, token) => state.ReadAsync(token),
                EntryOptions,
                cancellationToken: ct);
        }
        catch (Exception exception) when (!lookup.DatabaseFailed
            && !(exception is OperationCanceledException && ct.IsCancellationRequested))
        {
            LogCacheReadFailed(logger, userId, exception.GetType().Name);
            return await ReadFromDatabaseAsync(userId, ct);
        }
    }

    // The stamp change is already committed, so a client disconnect must not skip the eviction; the evictions
    // use their own timeout so a slow Redis does not stall the response.
    public Task InvalidateAsync(Guid userId) => evictions.EvictAsync(userId);

    private Task<string?> ReadFromDatabaseAsync(Guid userId, CancellationToken ct) =>
        db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp)
            .FirstOrDefaultAsync(ct);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Security stamp cache unavailable for user {UserId} ({ErrorType}), reading from the database")]
    private static partial void LogCacheReadFailed(ILogger logger, Guid userId, string errorType);

    private sealed class Lookup(SecurityStampCache owner, Guid userId)
    {
        public bool DatabaseFailed { get; private set; }

        public async ValueTask<string?> ReadAsync(CancellationToken ct)
        {
            try
            {
                return await owner.ReadFromDatabaseAsync(userId, ct);
            }
            catch
            {
                DatabaseFailed = true;
                throw;
            }
        }
    }
}
