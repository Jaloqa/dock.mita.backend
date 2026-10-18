using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Caching;

namespace Mita.Api.Behaviors;

public sealed partial class CacheInvalidationBehavior<TRequest, TResponse>(
    HybridCache cache,
    ICurrentUser currentUser,
    ILogger<CacheInvalidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IInvalidatesCache
    where TResponse : IFailableResult<TResponse>
{
    private static readonly TimeSpan InvalidationTimeout = TimeSpan.FromSeconds(2);

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var response = await next(cancellationToken);
        if (!response.IsSuccess)
        {
            return response;
        }

        string[] userTags = request.InvalidatesAllUserEntries ? [CacheKeys.UserTag(userId)] : [];
        string[] tags = [.. request.CacheTagsToInvalidate.Select(tag => CacheKeys.Tag(userId, tag)), .. userTags];
        // The command is already committed, so a client disconnect must not skip invalidation; the timeout keeps a slow Redis from stalling the response.
        using var timeout = new CancellationTokenSource(InvalidationTimeout);
        try
        {
            await cache.RemoveByTagAsync(tags, timeout.Token);
        }
        catch (Exception exception)
        {
            LogInvalidationFailed(logger, exception, string.Join(", ", tags), typeof(TRequest).Name);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not invalidate cache tags {CacheTags} after {RequestName}")]
    private static partial void LogInvalidationFailed(ILogger logger, Exception exception, string cacheTags, string requestName);
}
