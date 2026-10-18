using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Infrastructure.Caching;

namespace Mita.Api.Behaviors;

public sealed partial class CachingBehavior<TRequest, TResponse>(
    HybridCache cache,
    ICurrentUser currentUser,
    ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICachedQuery
    where TResponse : IFailableResult<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string key;
        string[] tags;
        if (request.IsUserScoped)
        {
            var userId = currentUser.Id;
            key = CacheKeys.Key(userId, request.CacheKey);
            tags = [CacheKeys.UserTag(userId), .. request.CacheTags.Select(tag => CacheKeys.Tag(userId, tag))];
        }
        else
        {
            key = CacheKeys.Global(request.CacheKey);
            tags = [.. request.CacheTags.Select(CacheKeys.Global)];
        }

        var invocation = new Invocation(next);

        try
        {
            return await cache.GetOrCreateAsync(
                key,
                invocation,
                static (state, token) => state.InvokeAsync(token),
                EntryOptions(request.CacheExpiration),
                tags,
                cancellationToken);
        }
        catch (FailedResultException failed)
        {
            return failed.Response;
        }
        catch (Exception exception) when (!invocation.HandlerThrew
            && !(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            LogCacheFailed(logger, exception, typeof(TRequest).Name);
            return invocation.HasResponse ? invocation.Response! : await next(cancellationToken);
        }
    }

    private static HybridCacheEntryOptions? EntryOptions(TimeSpan? expiration) =>
        expiration is { } value
            ? new HybridCacheEntryOptions
            {
                Expiration = value,
                LocalCacheExpiration = value < CacheDefaults.LocalExpiration ? value : CacheDefaults.LocalExpiration
            }
            : null;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cache unavailable for {RequestName}, calling the handler directly")]
    private static partial void LogCacheFailed(ILogger logger, Exception exception, string requestName);

    private sealed class Invocation(RequestHandlerDelegate<TResponse> next)
    {
        private readonly ExecutionContext? callerContext = ExecutionContext.Capture();

        public bool HandlerThrew { get; private set; }

        public bool HasResponse { get; private set; }

        public TResponse? Response { get; private set; }

        // HybridCache runs the factory without the caller's ExecutionContext, which would lose AsyncLocal
        // state such as the current HttpContext that handlers read through ICurrentUser.
        public ValueTask<TResponse> InvokeAsync(CancellationToken cancellationToken)
        {
            if (callerContext is null)
            {
                return InvokeHandlerAsync(cancellationToken);
            }

            ValueTask<TResponse> pending = default;
            ExecutionContext.Run(callerContext, _ => pending = InvokeHandlerAsync(cancellationToken), null);
            return pending;
        }

        private async ValueTask<TResponse> InvokeHandlerAsync(CancellationToken cancellationToken)
        {
            TResponse response;
            try
            {
                response = await next(cancellationToken);
            }
            catch
            {
                HandlerThrew = true;
                throw;
            }

            (Response, HasResponse) = (response, true);

            // HybridCache cannot skip storing a value returned by the factory; an exception is never stored.
            return response.IsSuccess ? response : throw new FailedResultException(response);
        }
    }

    private sealed class FailedResultException(TResponse response)
        : Exception("A failed result is returned without being cached.")
    {
        public TResponse Response { get; } = response;
    }
}
