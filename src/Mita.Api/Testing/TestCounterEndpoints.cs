using System.Collections.Concurrent;
using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Extensions;

namespace Mita.Api.Testing;

public sealed record CounterQuery(string Name) : IRequest<Result<int>>, ICachedQuery
{
    public const string Tag = "counter";

    public string CacheKey => $"counter:{Name}";

    public IReadOnlyCollection<string> CacheTags => [Tag];

    public TimeSpan? CacheExpiration => null;
}

public sealed record ResetCounterCommand : IRequest<Result>, IInvalidatesCache
{
    public IReadOnlyCollection<string> CacheTagsToInvalidate => [CounterQuery.Tag];
}

public sealed record CounterResponse(int Value);

public sealed class CounterQueryHandler(ICurrentUser currentUser) : IRequestHandler<CounterQuery, Result<int>>
{
    private static readonly ConcurrentDictionary<(Guid UserId, string Name), int> Counters = new();

    public Task<Result<int>> Handle(CounterQuery request, CancellationToken cancellationToken) =>
        Task.FromResult<Result<int>>(Counters.AddOrUpdate((currentUser.Id, request.Name), 1, (_, value) => value + 1));
}

public sealed class ResetCounterCommandHandler : IRequestHandler<ResetCounterCommand, Result>
{
    public Task<Result> Handle(ResetCounterCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

public static class TestCounterEndpoints
{
    public static IEndpointRouteBuilder MapTestCounterEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var counter = endpoints.MapV1().MapGroup("/test/counter").RequireAuthorization(AuthExtensions.UserPolicy);

        counter.MapGet("", async (string name, IMediator mediator, CancellationToken cancellationToken) =>
            (await mediator.Send(new CounterQuery(name), cancellationToken))
                .ToHttp(value => TypedResults.Ok(new CounterResponse(value))));

        counter.MapPost("/reset", async (IMediator mediator, CancellationToken cancellationToken) =>
            (await mediator.Send(new ResetCounterCommand(), cancellationToken))
                .ToHttp(TypedResults.NoContent));

        return endpoints;
    }
}
