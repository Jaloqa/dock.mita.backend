using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class ReadinessTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    [Fact]
    public async Task Should_ReportReady_When_DatabaseIsUp()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_ReportNotReadyButAlive_When_DatabaseIsDown()
    {
        await using var unreachable = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            (_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mita"] = "Host=127.0.0.1;Port=1;Database=mita;Timeout=2"
            })));
        using var client = unreachable.CreateClient();

        using var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        using var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void Should_FailToStart_When_ConnectionStringIsMissing()
    {
        using var misconfigured = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            (_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mita"] = ""
            })));

        Should.Throw<Microsoft.Extensions.Options.OptionsValidationException>(() => misconfigured.CreateClient());
    }
}
