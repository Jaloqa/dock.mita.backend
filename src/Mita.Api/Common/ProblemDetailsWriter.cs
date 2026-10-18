using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Mita.Api.Common;

internal static class ProblemDetailsWriter
{
    private const string ProblemJson = "application/problem+json";

    public static Task WriteStatusAsync(HttpContext httpContext, int status)
    {
        httpContext.Response.StatusCode = status;

        return WriteAsync(
            httpContext,
            new ProblemDetails { Status = status, Title = ReasonPhrases.GetReasonPhrase(status) });
    }

    public static async Task WriteAsync(HttpContext httpContext, ProblemDetails problem)
    {
        var services = httpContext.RequestServices;
        var context = new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem };

        if (await services.GetRequiredService<IProblemDetailsService>().TryWriteAsync(context))
        {
            return;
        }

        // The negotiated media type can exclude JSON, but the error contract is always problem+json.
        services.GetService<IOptions<ProblemDetailsOptions>>()?.Value.CustomizeProblemDetails?.Invoke(context);
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            problem.GetType(),
            services.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions,
            ProblemJson,
            httpContext.RequestAborted);
    }
}
