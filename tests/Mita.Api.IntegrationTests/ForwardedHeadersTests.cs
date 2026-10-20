using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class ForwardedHeadersTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string Proxy = "10.0.0.5";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("ForwardedHeaders:KnownProxies:0", Proxy)]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/24")]
    public async Task Should_LimitEachForwardedClientSeparately_When_ProxyIsTrusted(string key, string value)
    {
        await using var app = Limited(Proxy, key, value);

        (await LoginStatusesAsync(app, "203.0.113.1", 3)).ShouldBe(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests]);
        (await LoginStatusesAsync(app, "203.0.113.2", 1)).ShouldBe([HttpStatusCode.Unauthorized]);
    }

    [Fact]
    public async Task Should_IgnoreForwardedFor_When_SourceIsNotTrusted()
    {
        await using var app = Limited("198.51.100.7", "ForwardedHeaders:KnownProxies:0", Proxy);

        (await LoginStatusesAsync(app, "203.0.113.1", 2)).ShouldBe(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized]);
        (await LoginStatusesAsync(app, "203.0.113.2", 1)).ShouldBe([HttpStatusCode.TooManyRequests]);
    }

    [Fact]
    public async Task Should_LimitEachClientSeparately_When_TwoTrustedHopsAndForwardLimitIsTwo()
    {
        await using var app = Limited(
            Proxy,
            "ForwardedHeaders:KnownNetworks:0",
            "10.0.0.0/24",
            new KeyValuePair<string, string?>("ForwardedHeaders:ForwardLimit", "2"));

        (await LoginStatusesAsync(app, "203.0.113.1, 10.0.0.9", 3)).ShouldBe(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests]);
        (await LoginStatusesAsync(app, "203.0.113.2, 10.0.0.9", 1)).ShouldBe([HttpStatusCode.Unauthorized]);
    }

    [Fact]
    public async Task Should_UseLastHopOnly_When_ForwardLimitIsDefault()
    {
        await using var app = Limited(Proxy, "ForwardedHeaders:KnownNetworks:0", "10.0.0.0/24");

        (await LoginStatusesAsync(app, "203.0.113.1, 10.0.0.9", 2)).ShouldBe(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized]);
        (await LoginStatusesAsync(app, "203.0.113.2, 10.0.0.9", 1)).ShouldBe([HttpStatusCode.TooManyRequests]);
    }

    [Fact]
    public async Task Should_IgnoreSpoofedEntry_When_ForwardLimitIsDefault()
    {
        await using var app = Limited(Proxy, "ForwardedHeaders:KnownNetworks:0", "10.0.0.0/24");

        (await LoginStatusesAsync(app, "198.51.100.1, 203.0.113.1", 2)).ShouldBe(
            [HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized]);
        (await LoginStatusesAsync(app, "198.51.100.2, 203.0.113.1", 1)).ShouldBe([HttpStatusCode.TooManyRequests]);
    }

    private WebApplicationFactory<Program> Limited(
        string remoteIp,
        string key,
        string value,
        params KeyValuePair<string, string?>[] extraSettings) =>
        factory
            .WithSettings(new Dictionary<string, string?>(extraSettings)
            {
                ["RateLimiting:AuthPermitLimit"] = "2",
                [key] = value,
            })
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(IPAddress.Parse(remoteIp)))));

    private static async Task<HttpStatusCode[]> LoginStatusesAsync(
        WebApplicationFactory<Program> app,
        string forwardedFor,
        int count)
    {
        using var client = new AuthClient(app);
        client.Http.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        var statuses = new HttpStatusCode[count];
        for (var attempt = 0; attempt < count; attempt++)
        {
            using var response = await client.LoginAsync("nobody@example.com", "wrong-password-123", Ct);
            statuses[attempt] = response.StatusCode;
        }

        return statuses;
    }

    // TestServer has no real peer, so the connection address is set here to play the proxy or a direct client.
    private sealed class RemoteIpStartupFilter(IPAddress remoteIp) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = remoteIp;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
