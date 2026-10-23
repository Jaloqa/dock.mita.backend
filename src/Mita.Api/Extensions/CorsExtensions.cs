using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Mita.Api.Authentication;
using Mita.Api.Common.Groups;
using Mita.Api.Configuration;
using Mita.Api.Middleware;

namespace Mita.Api.Extensions;

public static class CorsExtensions
{
    private static readonly string[] AllowedHeaders =
    [
        "Content-Type", AuthCookies.XsrfHeader, "Accept-Language", CorrelationIdMiddleware.HeaderName,
        GroupAccess.MemberHeader
    ];

    private static readonly string[] AllowedMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    public static IServiceCollection AddMitaCors(this IServiceCollection services)
    {
        services.AddOptions<CorsOriginsOptions>()
            .BindConfiguration(CorsOriginsOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<CorsOriginsOptions>, CorsOriginsOptionsValidator>();

        services.AddCors();
        services.AddOptions<CorsOptions>()
            .Configure<IOptions<CorsOriginsOptions>>((cors, origins) => cors.AddDefaultPolicy(policy => policy
                .WithOrigins(origins.Value.AllowedOrigins)
                .WithHeaders(AllowedHeaders)
                .WithMethods(AllowedMethods)
                .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)
                .AllowCredentials()));

        return services;
    }
}
