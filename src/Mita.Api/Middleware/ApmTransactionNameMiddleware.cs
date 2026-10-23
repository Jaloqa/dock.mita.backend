using Elastic.Apm.Api;
using Mita.Api.Common.Logging;

namespace Mita.Api.Middleware;

public sealed class ApmTransactionNameMiddleware(RequestDelegate next, ITracer tracer)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (tracer.CurrentTransaction is { } transaction && NameOf(context) is { } name)
        {
            transaction.Name = name;
        }

        return next(context);
    }

    private static string? NameOf(HttpContext context)
    {
        var method = context.Request.Method;
        return context.GetEndpoint() switch
        {
            RouteEndpoint { RoutePattern.RawText: { } pattern } => $"{method} {pattern}",
            // Without a route (405, unsupported API version) the agent keeps "{method} {path}", token included.
            _ when ShareLinkRedaction.ContainsToken(context.Request.Path.Value) => $"{method} unknown route",
            _ => null
        };
    }
}
