using System.Net;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class OpenApiTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    [Fact]
    public async Task Should_ServeOpenApiDocument()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var info = document.RootElement.GetProperty("info");
        info.GetProperty("title").GetString().ShouldBe("Mita API");
        info.GetProperty("version").GetString().ShouldBe("v1");
    }

    [Fact]
    public async Task Should_NotServeScalar_OutsideDevelopment()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
