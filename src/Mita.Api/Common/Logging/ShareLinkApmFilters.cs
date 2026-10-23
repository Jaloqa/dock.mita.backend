using Elastic.Apm;
using Elastic.Apm.Api;
using Elastic.Apm.Report;

namespace Mita.Api.Common.Logging;

public sealed partial class ShareLinkApmFilters(IApmAgent agent, ILogger<ShareLinkApmFilters> logger) : IHostedService
{
    private int errorContextMissingLogged;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var sender = agent.PayloadSender as IPayloadSenderWithFilters;
        if (sender?.AddFilter(RedactTransaction) != true)
        {
            LogFilterRefused(logger, "transaction");
        }

        if (sender?.AddFilter(RedactError) != true)
        {
            LogFilterRefused(logger, "error");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public static ITransaction RedactTransaction(ITransaction transaction)
    {
        transaction.Name = ShareLinkRedaction.Redact(transaction.Name);
        Redact(transaction.Context);
        return transaction;
    }

    public IError RedactError(IError error)
    {
        // IError hides the request context the agent copies into each captured error, so the runtime type is asked.
        var property = error.GetType().GetProperty(nameof(Context));
        if (property is null || !typeof(Context).IsAssignableFrom(property.PropertyType))
        {
            if (Interlocked.Exchange(ref errorContextMissingLogged, 1) == 0)
            {
                LogErrorContextMissing(logger, error.GetType());
            }

            return error;
        }

        Redact(property.GetValue(error) as Context);
        return error;
    }

    private static void Redact(Context? context)
    {
        if (context?.Request?.Url is not { } url)
        {
            return;
        }

        url.Full = ShareLinkRedaction.Redact(url.Full);
        url.PathName = ShareLinkRedaction.Redact(url.PathName);
        url.Raw = ShareLinkRedaction.Redact(url.Raw);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "APM did not accept the {Filter} filter that masks share tokens")]
    private static partial void LogFilterRefused(ILogger logger, string filter);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "APM error {ErrorType} has no request Context property, so share tokens in errors are not masked")]
    private static partial void LogErrorContextMissing(ILogger logger, Type errorType);
}
