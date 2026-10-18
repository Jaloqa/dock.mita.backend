using System.Net;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class ErrorHandlingTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    [Fact]
    public async Task Should_ReturnProblemJson_When_AcceptExcludesJson()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/xml");

        using var response = await client.GetAsync("/test/throw", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("title").GetString().ShouldBe("Server error");
        json.RootElement.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Should_ReturnNotFoundProblem_When_RouteDoesNotExist()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/v1/does-not-exist", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(404);
        json.RootElement.GetProperty("title").GetString().ShouldBe("Not Found");
        json.RootElement.GetProperty("correlationId").GetString()
            .ShouldBe(response.Headers.GetValues("X-Correlation-Id").ShouldHaveSingleItem());
    }

    [Fact]
    public async Task Should_ReturnMethodNotAllowedProblem_When_MethodDoesNotMatch()
    {
        using var client = factory.CreateClient();

        using var response = await client.DeleteAsync("/test/echo", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("title").GetString().ShouldBe("Method Not Allowed");
    }

    [Fact]
    public async Task Should_ReturnProblemDetails_When_HandlerThrows()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/test/throw", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldNotContain("exception", Case.Insensitive);
        body.ShouldNotContain("stackTrace", Case.Insensitive);
        body.ShouldNotContain("detail", Case.Insensitive);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("title").GetString().ShouldBe("Server error");
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(500);
        json.RootElement.GetProperty("correlationId").GetString()
            .ShouldBe(response.Headers.GetValues("X-Correlation-Id").ShouldHaveSingleItem());
    }
}
