using Asp.Versioning;

namespace Mita.Api.Extensions;

public static class ApiVersioningExtensions
{
    public const string UserPolicy = "User";

    public static IServiceCollection AddMitaApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(UserPolicy, policy => policy.RequireAuthenticatedUser());

        return services;
    }

    public static RouteGroupBuilder MapV1(this IEndpointRouteBuilder app) =>
        app.NewVersionedApi()
            .MapGroup("/v{version:apiVersion}")
            .HasApiVersion(new ApiVersion(1, 0))
            .RequireAuthorization(UserPolicy);
}
