using System.Text.RegularExpressions;
using Serilog.Context;

namespace Mita.Api.Middleware;

public sealed partial class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var inbound = context.Request.Headers[HeaderName].ToString();
        var correlationId = AcceptedPattern().IsMatch(inbound) ? inbound : Guid.NewGuid().ToString();

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    [GeneratedRegex(@"\A[A-Za-z0-9-]{1,64}\z")]
    private static partial Regex AcceptedPattern();
}
