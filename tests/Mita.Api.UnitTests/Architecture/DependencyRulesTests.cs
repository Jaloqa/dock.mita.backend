using Shouldly;

namespace Mita.Api.UnitTests.Architecture;

public sealed class DependencyRulesTests
{
    [Fact]
    public void Should_HaveNoProjectReferences_When_InspectingDomain() =>
        typeof(Mita.Domain.Common.IAuditable).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name).ShouldNotContain(name => name!.StartsWith("Mita.", StringComparison.Ordinal));

    [Fact]
    public void Should_NotReferenceApi_When_InspectingInfrastructure() =>
        typeof(Mita.Infrastructure.DependencyInjection).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name).ShouldNotContain("Mita.Api");
}
