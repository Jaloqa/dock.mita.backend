using System.Net;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Auth;

public sealed class SessionRateLimitTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_NotThrottleRefreshOrLogout_When_LoginLimitExhausted()
    {
        await using var limited = factory.WithSettings(
            new Dictionary<string, string?> { ["RateLimiting:AuthPermitLimit"] = "10" });
        using var client = new AuthClient(limited);

        for (var request = 0; request < 10; request++)
        {
            using var allowed = await client.LoginAsync("nobody@example.com", "wrong-password-123", Ct);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var throttled = await client.LoginAsync("nobody@example.com", "wrong-password-123", Ct))
        {
            throttled.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        }

        using var refresh = await client.RefreshAsync(Ct);
        using var logout = await client.LogoutAsync(ct: Ct);

        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Should_Return429WithRetryAfter_When_TooManySessionRequests()
    {
        await using var limited = factory.WithSettings(
            new Dictionary<string, string?> { ["RateLimiting:SessionPermitLimit"] = "2" });
        using var client = new AuthClient(limited);

        for (var request = 0; request < 2; request++)
        {
            using var allowed = await client.RefreshAsync(Ct);
            allowed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var rejected = await client.RefreshAsync(Ct);
        using var login = await client.LoginAsync("nobody@example.com", "wrong-password-123", Ct);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("register", "auth")]
    [InlineData("login", "auth")]
    [InlineData("refresh", "session")]
    [InlineData("logout", "session")]
    public void Should_UseRateLimitPolicy_When_AuthEndpointMapped(string action, string policy)
    {
        var endpoint = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == $"/v{{version:apiVersion}}/auth/{action}");

        endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>().ShouldNotBeNull().PolicyName.ShouldBe(policy);
    }
}
