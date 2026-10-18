using System.Net;
using System.Text;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class RequestBindingTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    [Theory]
    [InlineData("{")]
    [InlineData("{\"value\":\"x\"}")]
    public async Task Should_Return400_When_BodyIsMalformedJson(string body)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/test/echo", content, TestContext.Current.CancellationToken);

        await AssertInvalidBodyAsync(response);
    }

    [Fact]
    public async Task Should_Return400_When_BodyIsMissing()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(string.Empty, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/test/echo", content, TestContext.Current.CancellationToken);

        await AssertInvalidBodyAsync(response);
    }

    [Fact]
    public async Task Should_ReturnBody_When_BodyIsValid()
    {
        using var client = factory.CreateClient();
        using var content = new StringContent("{\"value\":7}", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync("/test/echo", content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("page", "x")]
    [InlineData("pageSize", "x")]
    public async Task Should_ReportParameterName_When_QueryParameterCannotBeBound(string name, string value)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/test/page?{name}={value}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("title").GetString().ShouldBe("Validation failed");
        var errors = json.RootElement.GetProperty("errors");
        errors.EnumerateObject().Select(p => p.Name).ShouldBe([name]);
        errors.GetProperty(name)[0].GetString().ShouldBe("validation.invalidValue");
    }

    [Fact]
    public async Task Should_ReturnDefaults_When_QueryIsEmpty()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/test/page", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe("{\"items\":[],\"page\":1,\"pageSize\":20,\"total\":0}");
    }

    private static async Task AssertInvalidBodyAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        json.RootElement.GetProperty("title").GetString().ShouldBe("Validation failed");
        json.RootElement.GetProperty("errors").GetProperty("body")[0].GetString()
            .ShouldBe("validation.invalidValue");
        json.RootElement.GetProperty("correlationId").GetString()
            .ShouldBe(response.Headers.GetValues("X-Correlation-Id").ShouldHaveSingleItem());
    }
}