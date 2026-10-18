using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Auth;

public sealed class XsrfTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string EchoPath = "/v1/test/echo-auth";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Accept_When_CookieAndMatchingHeader()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, EchoPath));
        using var response = await client.Http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Guid.TryParse(body.RootElement.GetProperty("userId").GetString(), out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Return403_When_CookieWithoutHeader()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await client.Http.PutAsync(EchoPath, null, Ct);

        await AssertXsrfRejectedAsync(response);
    }

    [Fact]
    public async Task Should_Return403_When_HeaderMismatch()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var request = new HttpRequestMessage(HttpMethod.Put, EchoPath);
        request.Headers.Add(AuthClient.XsrfHeader, "not-the-token");
        using var response = await client.Http.SendAsync(request, Ct);

        await AssertXsrfRejectedAsync(response);
    }

    [Fact]
    public async Task Should_Return403_When_HeaderEmpty()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var request = new HttpRequestMessage(HttpMethod.Put, EchoPath);
        request.Headers.TryAddWithoutValidation(AuthClient.XsrfHeader, string.Empty);
        using var response = await client.Http.SendAsync(request, Ct);

        await AssertXsrfRejectedAsync(response);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Should_Return403_When_UnsafeMethodWithoutHeader(string method)
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var request = new HttpRequestMessage(new HttpMethod(method), EchoPath);
        using var response = await client.Http.SendAsync(request, Ct);

        await AssertXsrfRejectedAsync(response);
    }

    [Fact]
    public async Task Should_Return403BeforeBinding_When_FormPostToJsonEndpointWithoutHeader()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("value", "1")]);
        using var response = await client.Http.PostAsync("/v1/test/echo-body-auth", content, Ct);

        await AssertXsrfRejectedAsync(response);
    }

    [Fact]
    public async Task Should_Return403BeforeBinding_When_MalformedJsonWithoutHeader()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var content = new StringContent("{not json", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.Http.PostAsync("/v1/test/echo-body-auth", content, Ct);

        await AssertXsrfRejectedAsync(response);
    }

    [Fact]
    public async Task Should_BindBody_When_JsonPostWithHeader()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, "/v1/test/echo-body-auth")
        {
            Content = JsonContent.Create(new { value = 7 })
        });
        using var response = await client.Http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_ChangeNothing_When_ResetRejectedForMissingHeader()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var path = $"/v1/test/counter?name={Guid.NewGuid():N}";

        var before = await ReadCounterAsync(client, path);
        using var rejected = await client.Http.PostAsync("/v1/test/counter/reset", null, Ct);
        var after = await ReadCounterAsync(client, path);

        await AssertXsrfRejectedAsync(rejected);
        after.ShouldBe(before);

        using var accepted = client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, "/v1/test/counter/reset"));
        using var acceptedResponse = await client.Http.SendAsync(accepted, Ct);
        acceptedResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ReadCounterAsync(client, path)).ShouldBe(before + 1);
    }

    [Fact]
    public async Task Should_Accept_When_BearerWithoutXsrf()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var bearer = client.Cookie("mita_at")!.Value;

        using var bare = new AuthClient(factory);
        using var request = new HttpRequestMessage(HttpMethod.Put, EchoPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        using var response = await bare.Http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_IgnoreCookie_When_AuthorizationHeaderPresent()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var request = new HttpRequestMessage(HttpMethod.Put, EchoPath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");
        using var response = await client.Http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_AllowGet_When_CookieWithoutHeader()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await client.Http.GetAsync("/v1/test/counter?name=xsrf", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_ExemptAuthEndpoints_When_CookieWithoutHeader()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await client.LogoutAsync(ct: Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<int> ReadCounterAsync(AuthClient client, string path)
    {
        using var response = await client.Http.GetAsync(path, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("value").GetInt32();
    }

    private static async Task AssertXsrfRejectedAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        body.RootElement.GetProperty("title").GetString().ShouldBe("Forbidden");
        body.RootElement.GetProperty("code").GetString().ShouldBe("auth.xsrf");
        body.RootElement.TryGetProperty("userId", out _).ShouldBeFalse();
    }
}
