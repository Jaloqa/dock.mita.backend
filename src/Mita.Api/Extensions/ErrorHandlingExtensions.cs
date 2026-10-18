using Mita.Api.Middleware;

namespace Mita.Api.Extensions;

public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddMitaErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            if (context.HttpContext.Items[CorrelationIdMiddleware.ItemKey] is string correlationId)
            {
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
            }
        });
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddExceptionHandler<BadRequestExceptionHandler>();
        services.AddExceptionHandler<UnhandledExceptionHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/test/throw",
            IResult () => throw new InvalidOperationException("Test failure")).AllowAnonymous();

        endpoints.MapPost("/test/echo", (EchoBody body) => Results.Ok(body)).AllowAnonymous();

        return endpoints;
    }
}
