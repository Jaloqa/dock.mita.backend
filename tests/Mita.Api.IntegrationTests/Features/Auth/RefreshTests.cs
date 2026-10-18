using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Infrastructure.Authentication;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Auth;

public sealed class RefreshTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_RotateCookies_When_Refreshing()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var access = client.Cookie("mita_at")!.Value;
        var refresh = client.Cookie("mita_rt", "/v1/auth/")!.Value;
        var xsrf = client.Xsrf;

        using var response = await client.RefreshAsync(Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBeEmpty();
        client.Cookie("mita_at")!.Value.ShouldNotBe(access);
        client.Cookie("mita_rt", "/v1/auth/")!.Value.ShouldNotBe(refresh);
        client.Xsrf.ShouldNotBeNullOrWhiteSpace();
        client.Xsrf.ShouldNotBe(xsrf);
    }

    [Fact]
    public async Task Should_KeepRotating_When_RefreshingRepeatedly()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        for (var round = 0; round < 3; round++)
        {
            using var response = await client.RefreshAsync(Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
    }

    [Fact]
    public async Task Should_Return401AndClearCookies_When_RefreshCookieMissing()
    {
        using var client = new AuthClient(factory);

        using var response = await client.RefreshAsync(Ct);

        await AssertSessionExpiredAsync(response);
    }

    [Fact]
    public async Task Should_Return401AndClearCookies_When_RefreshTokenUnknown()
    {
        using var client = new AuthClient(factory);
        client.SetRefreshCookie("not-a-real-token");

        using var response = await client.RefreshAsync(Ct);

        await AssertSessionExpiredAsync(response);
        client.Cookie("mita_rt", "/v1/auth/").ShouldBeNull();
    }

    [Fact]
    public async Task Should_RevokeFamilyAndReturn401_When_OldRefreshReused()
    {
        var clock = new TestTimeProvider();
        await using var timed = WithClock(clock);
        using var victim = new AuthClient(timed);
        await victim.RegisterNewAsync(Ct);
        var previous = victim.Cookie("mita_rt", "/v1/auth/")!.Value;
        using (var rotated = await victim.RefreshAsync(Ct))
        {
            rotated.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        clock.Advance(TimeSpan.FromSeconds(TokenService.RotationGracePeriod.TotalSeconds + 1));
        using var thief = new AuthClient(timed);
        thief.SetRefreshCookie(previous);

        using var replay = await thief.RefreshAsync(Ct);
        using var current = await victim.RefreshAsync(Ct);

        await AssertSessionExpiredAsync(replay);
        await AssertSessionExpiredAsync(current);
    }

    [Fact]
    public async Task Should_Return204WithoutCookiesAndKeepSession_When_OldRefreshReplayedWithinGrace()
    {
        var clock = new TestTimeProvider();
        await using var timed = WithClock(clock);
        using var client = new AuthClient(timed);
        await client.RegisterNewAsync(Ct);
        var previous = client.Cookie("mita_rt", "/v1/auth/")!.Value;
        using (var rotated = await client.RefreshAsync(Ct))
        {
            rotated.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        clock.Advance(TimeSpan.FromSeconds(5));
        using var racing = new AuthClient(timed);
        racing.SetRefreshCookie(previous);

        using var replay = await racing.RefreshAsync(Ct);
        using var current = await client.RefreshAsync(Ct);

        replay.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        replay.Headers.Contains("Set-Cookie").ShouldBeFalse();
        current.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Should_KeepSharedCookieJarWorking_When_LosingRefreshResponseArrivesLast()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var previous = client.Cookie("mita_rt", "/v1/auth/")!.Value;

        using (var winner = await client.RefreshAsync(Ct))
        {
            winner.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var bare = factory.CreateDefaultClient(AuthClient.BaseAddress);
        using var loserRequest = new HttpRequestMessage(HttpMethod.Post, AuthClient.RefreshPath);
        loserRequest.Headers.Add("Cookie", $"mita_rt={previous}");
        using var loser = await bare.SendAsync(loserRequest, Ct);
        if (loser.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var setCookie in setCookies)
            {
                client.Cookies.SetCookies(AuthClient.BaseAddress, setCookie);
            }
        }

        loser.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var next = await client.RefreshAsync(Ct);
        next.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        client.Cookie("mita_at").ShouldNotBeNull();
        client.Cookie("mita_rt", "/v1/auth/").ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_Return401_When_RefreshTokenExpired()
    {
        var clock = new TestTimeProvider();
        await using var timed = WithClock(clock);
        using var client = new AuthClient(timed);
        await client.RegisterNewAsync(Ct);

        clock.Advance(TokenService.RefreshLifetime + TimeSpan.FromMinutes(1));
        using var response = await client.RefreshAsync(Ct);

        await AssertSessionExpiredAsync(response);
    }

    internal static async Task AssertSessionExpiredAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("title").GetString().ShouldBe("Unauthorized");
        problem.GetProperty("code").GetString().ShouldBe("auth.sessionExpired");
        AssertCookiesCleared(response);
    }

    internal static void AssertCookiesCleared(HttpResponseMessage response)
    {
        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        foreach (var name in new[] { "mita_at", "mita_rt", "XSRF-TOKEN" })
        {
            cookies.ShouldContain(
                cookie => cookie.StartsWith(name + "=;", StringComparison.Ordinal)
                    && cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        }
    }

    private WebApplicationFactory<Program> WithClock(TimeProvider clock) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton(clock)));
}
