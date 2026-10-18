using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Mita.Infrastructure;

namespace Mita.Api.Extensions;

public static class HealthExtensions
{
    public const string LiveTag = "live";

    public static IServiceCollection AddMitaHealth(this IServiceCollection services)
    {
        services.AddHealthChecks();
        return services;
    }

    public static IEndpointRouteBuilder MapMitaHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag)
        }).AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(DependencyInjection.ReadyTag)
        }).AllowAnonymous();

        return endpoints;
    }
}
