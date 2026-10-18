using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mita.Api.Authentication;
using Mita.Api.Behaviors;
using Mita.Api.Common;
using Mita.Api.Common.Caching;
using Mita.Api.Common.Rates;
using Mita.Infrastructure.Caching;
using Moq;
using Shouldly;

namespace Mita.Api.UnitTests.Behaviors;

public sealed class CachingBehaviorTests : IDisposable
{
    private readonly ServiceProvider provider = new ServiceCollection()
        .AddHybridCache()
        .AddSerializerFactory<ResultCacheSerializerFactory>()
        .Services
        .BuildServiceProvider();
    private readonly Mock<ISampleHandler> handler = new();

    private HybridCache Cache => provider.GetRequiredService<HybridCache>();

    public void Dispose() => provider.Dispose();

    [Fact]
    public async Task Should_CallHandlerOnce_When_SameUserAsksTwice()
    {
        handler.Setup(h => h.Handle()).Returns("value");
        var behavior = CreateBehavior(Guid.NewGuid());

        var first = await behavior.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);
        var second = await behavior.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);

        first.Value.ShouldBe("value");
        second.Value.ShouldBe("value");
        handler.Verify(h => h.Handle(), Times.Once);
    }

    [Fact]
    public async Task Should_CallHandlerPerUser_When_DifferentUsersAskSameQuery()
    {
        handler.SetupSequence(h => h.Handle()).Returns("first user").Returns("second user");
        var firstUser = CreateBehavior(Guid.NewGuid());
        var secondUser = CreateBehavior(Guid.NewGuid());

        var first = await firstUser.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);
        var second = await secondUser.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);
        var firstAgain = await firstUser.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);
        var secondAgain = await secondUser.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);

        first.Value.ShouldBe("first user");
        second.Value.ShouldBe("second user");
        firstAgain.Value.ShouldBe("first user");
        secondAgain.Value.ShouldBe("second user");
        handler.Verify(h => h.Handle(), Times.Exactly(2));
    }

    [Fact]
    public async Task Should_NotCache_When_ResultFailed()
    {
        handler.SetupSequence(h => h.Handle()).Returns(Error.NotFound()).Returns("found");
        var behavior = CreateBehavior(Guid.NewGuid());

        var first = await behavior.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);
        var second = await behavior.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);

        first.Error.ShouldBe(Error.NotFound());
        second.Value.ShouldBe("found");
        handler.Verify(h => h.Handle(), Times.Exactly(2));
    }

    [Fact]
    public async Task Should_CallHandlerAgain_When_TagInvalidated()
    {
        handler.SetupSequence(h => h.Handle()).Returns("old").Returns("new");
        var userId = Guid.NewGuid();
        var behavior = CreateBehavior(userId);
        var invalidation = new CacheInvalidationBehavior<SampleCommand, Result>(
            Cache,
            CurrentUser(userId),
            NullLogger<CacheInvalidationBehavior<SampleCommand, Result>>.Instance);

        var before = await behavior.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);
        await invalidation.Handle(
            new SampleCommand(),
            _ => Task.FromResult(Result.Success()),
            TestContext.Current.CancellationToken);
        var after = await behavior.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);

        before.Value.ShouldBe("old");
        after.Value.ShouldBe("new");
        handler.Verify(h => h.Handle(), Times.Exactly(2));
    }

    [Fact]
    public async Task Should_CallHandlerAgain_When_AllUserEntriesInvalidated()
    {
        handler.SetupSequence(h => h.Handle()).Returns("old").Returns("other user").Returns("new");
        var userId = Guid.NewGuid();
        var behavior = CreateBehavior(userId);
        var otherUser = CreateBehavior(Guid.NewGuid());
        var invalidation = new CacheInvalidationBehavior<ForgetUserCommand, Result>(
            Cache,
            CurrentUser(userId),
            NullLogger<CacheInvalidationBehavior<ForgetUserCommand, Result>>.Instance);

        var before = await behavior.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);
        await otherUser.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);
        await invalidation.Handle(
            new ForgetUserCommand(),
            _ => Task.FromResult(Result.Success()),
            TestContext.Current.CancellationToken);
        var after = await behavior.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);
        var otherAfter = await otherUser.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);

        before.Value.ShouldBe("old");
        after.Value.ShouldBe("new");
        otherAfter.Value.ShouldBe("other user");
    }

    [Fact]
    public async Task Should_CallHandler_When_CacheThrows()
    {
        handler.Setup(h => h.Handle()).Returns("value");
        var failingCache = new Mock<HybridCache>();
        failingCache
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Func<It.IsAnyType, CancellationToken, ValueTask<It.IsAnyType>>>(),
                It.IsAny<HybridCacheEntryOptions?>(),
                It.IsAny<IEnumerable<string>?>(),
                It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Cache unavailable"));
        var behavior = new CachingBehavior<SampleQuery, Result<string>>(
            failingCache.Object,
            CurrentUser(Guid.NewGuid()),
            NullLogger<CachingBehavior<SampleQuery, Result<string>>>.Instance);

        var result = await behavior.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);

        result.Value.ShouldBe("value");
        handler.Verify(h => h.Handle(), Times.Once);
    }

    [Fact]
    public async Task Should_PropagateExceptionAndCallHandlerOnce_When_HandlerThrows()
    {
        var calls = 0;
        var behavior = CreateBehavior(Guid.NewGuid());

        await Should.ThrowAsync<InvalidOperationException>(() => behavior.Handle(
            new SampleQuery("a"),
            _ =>
            {
                calls++;
                throw new InvalidOperationException("Handler failed");
            },
            TestContext.Current.CancellationToken));

        calls.ShouldBe(1);
    }

    [Fact]
    public async Task Should_ReturnCapturedResponse_When_CacheThrowsAfterHandlerRan()
    {
        var calls = 0;
        var behavior = new CachingBehavior<SampleQuery, Result<string>>(
            new ThrowsAfterFactoryCache(),
            CurrentUser(Guid.NewGuid()),
            NullLogger<CachingBehavior<SampleQuery, Result<string>>>.Instance);

        var result = await behavior.Handle(
            new SampleQuery("a"),
            _ =>
            {
                calls++;
                return Task.FromResult<Result<string>>("value");
            },
            TestContext.Current.CancellationToken);

        result.Value.ShouldBe("value");
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task Should_RethrowCancellation_When_CallerCancels()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var behavior = CreateBehavior(Guid.NewGuid());

        await Should.ThrowAsync<OperationCanceledException>(() => behavior.Handle(
            new SampleQuery("a"),
            Next,
            source.Token));

        handler.Verify(h => h.Handle(), Times.Never);
    }

    [Fact]
    public async Task Should_InvalidateTags_When_RequestTokenIsCancelled()
    {
        handler.SetupSequence(h => h.Handle()).Returns("old").Returns("new");
        var userId = Guid.NewGuid();
        var query = CreateBehavior(userId);
        var invalidation = CreateInvalidation(Cache, userId);
        await query.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await invalidation.Handle(new SampleCommand(), _ => Task.FromResult(Result.Success()), source.Token);
        var after = await query.Handle(new SampleQuery("a"), Next, TestContext.Current.CancellationToken);

        after.Value.ShouldBe("new");
    }

    [Fact]
    public async Task Should_ReturnResponse_When_InvalidationFails()
    {
        var failingCache = new Mock<HybridCache>();
        failingCache
            .Setup(c => c.RemoveByTagAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Cache unavailable"));
        var invalidation = CreateInvalidation(failingCache.Object, Guid.NewGuid());

        var result = await invalidation.Handle(
            new SampleCommand(),
            _ => Task.FromResult(Result.Success()),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ReadUserBeforeHandler_When_CommandHasNoUser()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.Id).Throws(new UnauthorizedAccessException());
        var invalidation = new CacheInvalidationBehavior<SampleCommand, Result>(
            Cache,
            currentUser.Object,
            NullLogger<CacheInvalidationBehavior<SampleCommand, Result>>.Instance);
        var handlerRan = false;

        await Should.ThrowAsync<UnauthorizedAccessException>(() => invalidation.Handle(
            new SampleCommand(),
            _ =>
            {
                handlerRan = true;
                return Task.FromResult(Result.Success());
            },
            TestContext.Current.CancellationToken));

        handlerRan.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_RunHandlerWithCallerAsyncLocalState_When_CacheMisses()
    {
        var callerState = new AsyncLocal<string>();
        callerState.Value = "caller";
        var behavior = CreateBehavior(Guid.NewGuid());

        var result = await behavior.Handle(
            new SampleQuery("a"),
            _ => Task.FromResult<Result<string>>(callerState.Value ?? "lost"),
            TestContext.Current.CancellationToken);

        result.Value.ShouldBe("caller");
    }

    [Fact]
    public async Task Should_UseGlobalKey_When_QueryIsNotUserScoped()
    {
        handler.SetupSequence(h => h.Handle()).Returns("first").Returns("second").Returns("third");
        var firstUser = CreateGlobalBehavior(Guid.NewGuid());
        var secondUser = CreateGlobalBehavior(Guid.NewGuid());

        var first = await firstUser.Handle(new GlobalQuery("a"), Next, TestContext.Current.CancellationToken);
        var second = await secondUser.Handle(new GlobalQuery("a"), Next, TestContext.Current.CancellationToken);
        await Cache.RemoveAsync("g:global:a", TestContext.Current.CancellationToken);
        var afterKeyRemoval = await secondUser.Handle(new GlobalQuery("a"), Next, TestContext.Current.CancellationToken);
        await Cache.RemoveByTagAsync("g:global", TestContext.Current.CancellationToken);
        var afterTagRemoval = await firstUser.Handle(new GlobalQuery("a"), Next, TestContext.Current.CancellationToken);

        first.Value.ShouldBe("first");
        second.Value.ShouldBe("first");
        afterKeyRemoval.Value.ShouldBe("second");
        afterTagRemoval.Value.ShouldBe("third");
        handler.Verify(h => h.Handle(), Times.Exactly(3));
    }

    [Fact]
    public void Should_MatchGlobalRatesTag_When_ApiTagIsPrefixed() =>
        GlobalCacheTags.Rates.ShouldBe(CacheKeys.Global(RateCacheTags.Rates));

    [Fact]
    public async Task Should_NotReadCurrentUser_When_QueryIsNotUserScoped()
    {
        handler.Setup(h => h.Handle()).Returns("value");
        var currentUser = new Mock<ICurrentUser>(MockBehavior.Strict);
        var behavior = new CachingBehavior<GlobalQuery, Result<string>>(
            Cache,
            currentUser.Object,
            NullLogger<CachingBehavior<GlobalQuery, Result<string>>>.Instance);

        var result = await behavior.Handle(new GlobalQuery("a"), Next, TestContext.Current.CancellationToken);

        result.Value.ShouldBe("value");
    }

    private CachingBehavior<GlobalQuery, Result<string>> CreateGlobalBehavior(Guid userId) =>
        new(Cache, CurrentUser(userId), NullLogger<CachingBehavior<GlobalQuery, Result<string>>>.Instance);

    private CachingBehavior<SampleQuery, Result<string>> CreateBehavior(Guid userId) =>
        new(Cache, CurrentUser(userId), NullLogger<CachingBehavior<SampleQuery, Result<string>>>.Instance);

    private static CacheInvalidationBehavior<SampleCommand, Result> CreateInvalidation(HybridCache cache, Guid userId) =>
        new(cache, CurrentUser(userId), NullLogger<CacheInvalidationBehavior<SampleCommand, Result>>.Instance);

    private static ICurrentUser CurrentUser(Guid userId) => Mock.Of<ICurrentUser>(u => u.Id == userId);

    private Task<Result<string>> Next(CancellationToken cancellationToken) => Task.FromResult(handler.Object.Handle());

    private sealed class ThrowsAfterFactoryCache : HybridCache
    {
        public override async ValueTask<T> GetOrCreateAsync<TState, T>(
            string key,
            TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
        {
            await factory(state, cancellationToken);
            throw new InvalidOperationException("Cache failed after the handler");
        }

        public override ValueTask SetAsync<T>(
            string key,
            T value,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    public interface ISampleHandler
    {
        Result<string> Handle();
    }

    public sealed record SampleQuery(string Name) : IRequest<Result<string>>, ICachedQuery
    {
        public string CacheKey => $"sample:{Name}";

        public IReadOnlyCollection<string> CacheTags => ["sample"];

        public TimeSpan? CacheExpiration => null;
    }

    public sealed record GlobalQuery(string Name) : IRequest<Result<string>>, ICachedQuery
    {
        public bool IsUserScoped => false;

        public string CacheKey => $"global:{Name}";

        public IReadOnlyCollection<string> CacheTags => ["global"];

        public TimeSpan? CacheExpiration => null;
    }

    public sealed record SampleCommand : IRequest<Result>, IInvalidatesCache
    {
        public IReadOnlyCollection<string> CacheTagsToInvalidate => ["sample"];
    }

    public sealed record ForgetUserCommand : IRequest<Result>, IInvalidatesCache
    {
        public IReadOnlyCollection<string> CacheTagsToInvalidate => [];

        public bool InvalidatesAllUserEntries => true;
    }
}
