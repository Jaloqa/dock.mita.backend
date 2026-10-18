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
        services.AddExceptionHandler<UnhandledExceptionHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/test/throw",
            IResult () => throw new InvalidOperationException("Test failure")).AllowAnonymous();

        return endpoints;
    }
}
