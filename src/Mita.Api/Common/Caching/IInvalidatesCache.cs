namespace Mita.Api.Common.Caching;

public interface IInvalidatesCache
{
    IReadOnlyCollection<string> CacheTagsToInvalidate { get; }

    bool InvalidatesAllUserEntries => false;
}
