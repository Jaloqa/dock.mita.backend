namespace Mita.Infrastructure.Caching;

public static class CacheDefaults
{
    public const string InstanceName = "mita:";

    public const int RedisTimeoutMilliseconds = 1000;

    public static readonly TimeSpan Expiration = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan LocalExpiration = TimeSpan.FromMinutes(1);
}
