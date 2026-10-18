namespace Mita.Api.Common.Caching;

public static class CacheKeys
{
    public static string Key(Guid userId, string key) => $"u:{userId}:{key}";

    public static string Tag(Guid userId, string tag) => $"u:{userId}:{tag}";

    public static string UserTag(Guid userId) => $"user:{userId}";
}
