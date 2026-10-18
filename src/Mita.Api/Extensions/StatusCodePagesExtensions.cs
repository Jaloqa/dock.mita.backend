using Mita.Api.Common;

namespace Mita.Api.Extensions;

public static class StatusCodePagesExtensions
{
    public static IApplicationBuilder UseMitaStatusCodePages(this IApplicationBuilder app) =>
        app.UseStatusCodePages(context =>
            ProblemDetailsWriter.WriteStatusAsync(context.HttpContext, context.HttpContext.Response.StatusCode));
}
