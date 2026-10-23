using Asp.Versioning;

namespace Mita.Api.Extensions;

public static class ApiVersioningExtensions
{
    public static IServiceCollection AddMitaApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        return services;
    }

    public static RouteGroupBuilder MapVersioned(this IEndpointRouteBuilder app) =>
        app.NewVersionedApi()
            .MapGroup("/v{version:apiVersion}")
            .HasApiVersion(new ApiVersion(1, 0));

    public static RouteGroupBuilder MapV1(this IEndpointRouteBuilder app) =>
        app.MapVersioned().RequireAuthorization(AuthExtensions.UserPolicy);
}
