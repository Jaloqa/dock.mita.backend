using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Mita.Api.Common;
using Mita.Api.Configuration;

namespace Mita.Api.Extensions;

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";
    public const string SessionPolicy = "session";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddMitaRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<RateLimitingOptions>, RateLimitingOptionsValidator>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthPolicy, context => PerClientIp(context, limits => limits.AuthPermitLimit));
            options.AddPolicy(SessionPolicy, context => PerClientIp(context, limits => limits.SessionPermitLimit));
            options.OnRejected = async (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await ProblemDetailsWriter.WriteAsync(
                    context.HttpContext,
                    new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Title = "Too many requests" });
            };
        });

        return services;
    }

    private static RateLimitPartition<string> PerClientIp(
        HttpContext context,
        Func<RateLimitingOptions, int> permitLimit)
    {
        var limit = permitLimit(context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value);
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit,
                Window = Window,
                QueueLimit = 0
            });
    }
}
