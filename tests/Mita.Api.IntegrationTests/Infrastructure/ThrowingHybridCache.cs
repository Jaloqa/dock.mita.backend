using Microsoft.Extensions.Caching.Hybrid;

namespace Mita.Api.IntegrationTests.Infrastructure;

public sealed class ThrowingHybridCache : HybridCache
{
    private int reads;

    public int Reads => Volatile.Read(ref reads);

    public override ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref reads);
        throw new InvalidOperationException("Cache unavailable.");
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Cache unavailable.");

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Cache unavailable.");

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Cache unavailable.");
}
