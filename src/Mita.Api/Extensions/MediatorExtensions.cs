using FluentValidation;
using Microsoft.Extensions.Caching.Hybrid;
using Mita.Api.Behaviors;
using Mita.Api.Common.Caching;

namespace Mita.Api.Extensions;

public static class MediatorExtensions
{
    public static IServiceCollection AddMitaMediator(this IServiceCollection services)
    {
        services.AddMediatR(options =>
        {
            options.RegisterServicesFromAssemblyContaining<Program>();
            options.AddOpenBehavior(typeof(LoggingBehavior<,>));
            options.AddOpenBehavior(typeof(ValidationBehavior<,>));
            options.AddOpenBehavior(typeof(CacheInvalidationBehavior<,>));
            options.AddOpenBehavior(typeof(CachingBehavior<,>));
        });
        services.AddValidatorsFromAssemblyContaining<Program>();
        services.AddSingleton<IHybridCacheSerializerFactory, ResultCacheSerializerFactory>();
        return services;
    }
}
