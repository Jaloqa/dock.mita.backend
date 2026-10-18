using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class AuthTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string PingPath = "/v1/system/ping";

    [Fact]
    public async Task Should_Return401_When_NoToken()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(PingPath, TestContext.Current.CancellationToken);

        await AssertUnauthorizedProblemAsync(response);
    }

    [Fact]
    public async Task Should_Return401_When_TokenExpired()
    {
        var token = TestTokens.Create(Guid.NewGuid(), expires: DateTimeOffset.UtcNow.AddMinutes(-5));

        await AssertPingUnauthorizedAsync(token);
    }

    [Fact]
    public async Task Should_Return401_When_SignatureInvalid()
    {
        var token = TestTokens.Create(Guid.NewGuid(), signingKey: "another-signing-key-0123456789-abcdef");

        await AssertPingUnauthorizedAsync(token);
    }

    [Fact]
    public async Task Should_Return401_When_SubClaimMissing()
    {
        var token = TestTokens.Create(Guid.NewGuid(), includeSub: false);

        await AssertPingUnauthorizedAsync(token);
    }

    [Fact]
    public async Task Should_Return401_When_IssuerWrong()
    {
        await AssertPingUnauthorizedAsync(TestTokens.Create(Guid.NewGuid(), issuer: "someone-else"));
    }

    [Fact]
    public async Task Should_Return401_When_AudienceWrong()
    {
        await AssertPingUnauthorizedAsync(TestTokens.Create(Guid.NewGuid(), audience: "another-api"));
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Should_Return401_When_SubIsNotAValidGuid(string rawSub)
    {
        await AssertPingUnauthorizedAsync(TestTokens.Create(Guid.NewGuid(), rawSub: rawSub));
    }

    [Fact]
    public async Task Should_Return401ProblemJson_When_AcceptExcludesJson()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/xml");

        using var response = await client.GetAsync(PingPath, TestContext.Current.CancellationToken);

        await AssertUnauthorizedProblemAsync(response);
    }

    [Fact]
    public async Task Should_Return401_When_TokenIsNotAJwt()
    {
        await AssertPingUnauthorizedAsync("not-a-token");
    }

    [Fact]
    public async Task Should_ReturnUserId_When_TokenValid()
    {
        var userId = Guid.NewGuid();
        using var client = CreateClient(TestTokens.Create(userId));

        using var response = await client.GetAsync(PingPath, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("userId").GetGuid().ShouldBe(userId);
    }

    private async Task AssertPingUnauthorizedAsync(string token)
    {
        using var client = CreateClient(token);

        using var response = await client.GetAsync(PingPath, TestContext.Current.CancellationToken);

        await AssertUnauthorizedProblemAsync(response);
    }

    private HttpClient CreateClient(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task AssertUnauthorizedProblemAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ShouldContain(challenge => challenge.Scheme == "Bearer");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("status").GetInt32().ShouldBe(401);
        problem.GetProperty("title").GetString().ShouldBe("Unauthorized");
        problem.GetProperty("correlationId").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
