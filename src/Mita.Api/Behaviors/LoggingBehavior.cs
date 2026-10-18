using System.Diagnostics;
using MediatR;
using Mita.Api.Common;

namespace Mita.Api.Behaviors;

public sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IFailableResult<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var started = Stopwatch.GetTimestamp();

        var response = await next(cancellationToken);

        var elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (response.IsSuccess)
        {
            LogHandled(logger, requestName, elapsedMilliseconds);
        }
        else
        {
            LogFailed(logger, requestName, elapsedMilliseconds, response.Error!.Code);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMilliseconds} ms")]
    private static partial void LogHandled(ILogger logger, string requestName, long elapsedMilliseconds);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Request {RequestName} failed in {ElapsedMilliseconds} ms with {ErrorCode}")]
    private static partial void LogFailed(ILogger logger, string requestName, long elapsedMilliseconds, string errorCode);
}
