using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.Authentication;
using Mita.Api.Behaviors;
using Mita.Api.Common;
using Mita.Api.Extensions;
using Mita.Api.Testing;
using Moq;
using Shouldly;

namespace Mita.Api.UnitTests.Behaviors;

public sealed class PipelineOrderTests
{
    [Fact]
    public void Should_RunCachingAfterValidation_When_RequestIsCachedQuery()
    {
        using var provider = BuildProvider();

        var behaviors = provider.GetServices<IPipelineBehavior<CounterQuery, Result<int>>>();

        behaviors.Select(b => b.GetType().GetGenericTypeDefinition()).ShouldBe(
        [
            typeof(LoggingBehavior<,>),
            typeof(ValidationBehavior<,>),
            typeof(CachingBehavior<,>)
        ]);
    }

    [Fact]
    public void Should_RunCacheInvalidationAfterValidation_When_RequestInvalidatesCache()
    {
        using var provider = BuildProvider();

        var behaviors = provider.GetServices<IPipelineBehavior<ResetCounterCommand, Result>>();

        behaviors.Select(b => b.GetType().GetGenericTypeDefinition()).ShouldBe(
        [
            typeof(LoggingBehavior<,>),
            typeof(ValidationBehavior<,>),
            typeof(CacheInvalidationBehavior<,>)
        ]);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection().AddLogging().AddMitaMediator();
        services.AddHybridCache();
        services.AddSingleton(Mock.Of<ICurrentUser>());
        return services.BuildServiceProvider();
    }
}
