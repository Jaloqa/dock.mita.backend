using System.Net;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class HealthTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    [Fact]
    public async Task Should_ReturnHealthy_When_LiveEndpointCalled()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }
}
