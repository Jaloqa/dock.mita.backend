namespace Mita.Api.Common.Caching;

public interface ICachedQuery
{
    string CacheKey { get; }

    IReadOnlyCollection<string> CacheTags { get; }

    TimeSpan? CacheExpiration { get; }

    bool IsUserScoped => true;

    bool IsCacheable => true;
}
