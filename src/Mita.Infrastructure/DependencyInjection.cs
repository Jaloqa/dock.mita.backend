using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Mita.Infrastructure.Authentication;
using Mita.Infrastructure.Caching;
using Mita.Infrastructure.Identity;
using Mita.Infrastructure.Persistence;
using Mita.Infrastructure.Persistence.Interceptors;
using Mita.Infrastructure.Rates;
using StackExchange.Redis;

namespace Mita.Infrastructure;

public static class DependencyInjection
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<DatabaseOptions>()
            .Configure<IConfiguration>((options, root) =>
            {
                options.ConnectionString = root.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty;
                options.ApplyMigrationsOnStartupRaw = root[DatabaseOptions.ApplyMigrationsOnStartupKey];
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();
        services.AddMitaIdentity();

        services.AddSingleton<AuditingInterceptor>();
        services.AddDbContext<AppDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString)
            .AddInterceptors(provider.GetRequiredService<AuditingInterceptor>()));

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgres", tags: [ReadyTag]);

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<SecurityStampCache>();
        services.AddSingleton<SecurityStampEvictions>();
        services.AddHostedService(provider => provider.GetRequiredService<SecurityStampEvictions>());

        services.AddCaching();
        services.AddRates();

        return services;
    }

    public static IServiceCollection AddRates(this IServiceCollection services)
    {
        services.AddOptions<RatesOptions>()
            .BindConfiguration(RatesOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RatesOptions>, RatesOptionsValidator>();

        services.AddHttpClient<IExchangeRateSource, NbpExchangeRateSource>((provider, client) =>
            {
                var baseUrl = provider.GetRequiredService<IOptions<RatesOptions>>().Value.NbpBaseUrl.AbsoluteUri;
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
            })
            .AddStandardResilienceHandler();

        services.AddScoped<RatesSynchronizer>();
        services.AddHostedService<RatesSyncService>();

        return services;
    }

    private static void AddCaching(this IServiceCollection services)
    {
        services.AddOptions<RedisOptions>()
            .Configure<IConfiguration>((options, root) =>
            {
                options.ConnectionString = root.GetConnectionString(RedisOptions.ConnectionStringName) ?? string.Empty;
                if (root[$"{RedisOptions.ConnectionStringName}:InstanceName"] is { Length: > 0 } instanceName)
                {
                    options.InstanceName = instanceName;
                }
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RedisOptions>, RedisOptionsValidator>();

        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var connectionString = provider.GetRequiredService<IOptions<RedisOptions>>().Value.ConnectionString;
            var redis = ConfigurationOptions.Parse(connectionString);
            redis.AbortOnConnectFail = false;
            redis.BacklogPolicy = BacklogPolicy.FailFast;
            if (!HasSetting(connectionString, "connectTimeout"))
            {
                redis.ConnectTimeout = CacheDefaults.RedisTimeoutMilliseconds;
            }

            if (!HasSetting(connectionString, "syncTimeout"))
            {
                redis.SyncTimeout = CacheDefaults.RedisTimeoutMilliseconds;
            }

            if (!HasSetting(connectionString, "asyncTimeout"))
            {
                redis.AsyncTimeout = CacheDefaults.RedisTimeoutMilliseconds;
            }

            return ConnectionMultiplexer.Connect(redis);
        });

        services.AddStackExchangeRedisCache(_ => { });
        services.AddOptions<RedisCacheOptions>()
            .Configure<IServiceProvider>((options, provider) =>
            {
                options.InstanceName = provider.GetRequiredService<IOptions<RedisOptions>>().Value.InstanceName;
                options.ConnectionMultiplexerFactory =
                    () => Task.FromResult(provider.GetRequiredService<IConnectionMultiplexer>());
            });

        services.AddHybridCache(options => options.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = CacheDefaults.Expiration,
            LocalCacheExpiration = CacheDefaults.LocalExpiration
        });

        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>("redis", failureStatus: HealthStatus.Degraded, tags: [ReadyTag]);
    }

    private static bool HasSetting(string connectionString, string name) =>
        connectionString.Split(',').Any(part => part.TrimStart().StartsWith(name + "=", StringComparison.OrdinalIgnoreCase));
}
