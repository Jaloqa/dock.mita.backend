using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Infrastructure.Authentication;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Auth;

public sealed class SessionInvalidationTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string MePath = "/v1/me";
    private const string PingPath = "/v1/system/ping";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Reject_OldAccessCookie_AfterLogout()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        await AssertMeOkAsync(client);
        var accessToken = client.Cookie("mita_at")!.Value;

        using (var logout = await client.LogoutAsync(ct: Ct))
        {
            logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await AssertReplayRejectedAsync(factory, accessToken);
    }

    [Fact]
    public async Task Should_Reject_OldAccessCookie_AfterAccountDelete()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        await AssertMeOkAsync(client);
        var accessToken = client.Cookie("mita_at")!.Value;

        using (var deleted = await SendMeAsync(client, HttpMethod.Delete, new { password = credentials.Password }))
        {
            deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await AssertReplayRejectedAsync(factory, accessToken);
    }

    [Fact]
    public async Task Should_Reject_OldAccessCookie_AfterEmailChange()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        await AssertMeOkAsync(client);
        var accessToken = client.Cookie("mita_at")!.Value;

        var newEmail = $"{Guid.NewGuid():N}@changed.example.com";
        using (var updated = await SendMeAsync(client, HttpMethod.Put, new { name = credentials.Name, email = newEmail }))
        {
            updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await AssertReplayRejectedAsync(factory, accessToken);
        client.Cookie("mita_at")!.Value.ShouldNotBe(accessToken);
        await AssertMeOkAsync(client);
    }

    [Fact]
    public async Task Should_StillWork_When_RedisDown()
    {
        await using var redisDown = factory.WithSettings(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Redis"] = "127.0.0.1:1"
        });
        using var client = new AuthClient(redisDown);
        await client.RegisterNewAsync(Ct);
        await AssertMeOkAsync(client);
        var accessToken = client.Cookie("mita_at")!.Value;

        using (var logout = await client.LogoutAsync(ct: Ct))
        {
            logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await AssertReplayRejectedAsync(redisDown, accessToken);
    }

    [Fact]
    public async Task Should_Reject_OldAccessCookie_When_InFlightReadRestoresOldStampAfterLogout()
    {
        var clock = new ManualTimeProvider();
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(clock)));
        using var client = new AuthClient(app);
        var credentials = await client.RegisterNewAsync(Ct);
        await AssertMeOkAsync(client);
        var accessToken = client.Cookie("mita_at")!.Value;
        var userId = await UserIdAsync(credentials);
        var oldStamp = await StoredStampAsync(userId);

        using (var logout = await client.LogoutAsync(ct: Ct))
        {
            logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await app.Services.GetRequiredService<HybridCache>()
            .SetAsync(SecurityStampCache.Key(userId), oldStamp, cancellationToken: Ct);
        using (var poisoned = await SendWithBearerAsync(app, MePath, accessToken))
        {
            poisoned.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        clock.Advance(SecurityStampEvictions.RepeatDelay);

        await WaitUntilRejectedAsync(app, accessToken);
        await AssertReplayRejectedAsync(app, accessToken);
    }

    [Fact]
    public async Task Should_ReadStampFromDatabase_When_CacheThrows()
    {
        var cache = new ThrowingHybridCache();
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<HybridCache>();
            services.AddSingleton<HybridCache>(cache);
        }));
        using var client = new AuthClient(app);
        await client.RegisterNewAsync(Ct);
        await AssertMeOkAsync(client);
        var accessToken = client.Cookie("mita_at")!.Value;

        using (var logout = await client.LogoutAsync(ct: Ct))
        {
            logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        await AssertReplayRejectedAsync(app, accessToken);
        cache.Reads.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Should_Return401SessionExpired_When_AccessTokenExpired()
    {
        var user = await TestTokens.CreateForNewUserAsync(factory);
        var token = TestTokens.Create(
            user.UserId,
            expires: DateTimeOffset.UtcNow.AddMinutes(-5),
            stamp: await StoredStampAsync(user.UserId));

        using var response = await SendWithBearerAsync(factory, MePath, token);

        await AssertSessionExpiredAsync(response);
    }

    [Fact]
    public async Task Should_Return401SessionExpired_When_StampClaimMissing()
    {
        var user = await TestTokens.CreateForNewUserAsync(factory);
        var token = TestTokens.Create(user.UserId);

        using var response = await SendWithBearerAsync(factory, PingPath, token);

        await AssertSessionExpiredAsync(response);
    }

    [Fact]
    public async Task Should_Return401SessionExpired_When_StampClaimEmpty()
    {
        var user = await TestTokens.CreateForNewUserAsync(factory);
        var token = TestTokens.Create(user.UserId, stamp: string.Empty);

        using var response = await SendWithBearerAsync(factory, PingPath, token);

        await AssertSessionExpiredAsync(response);
    }

    [Fact]
    public async Task Should_Return401SessionExpired_When_StampDiffers()
    {
        var user = await TestTokens.CreateForNewUserAsync(factory);
        var token = TestTokens.Create(user.UserId, stamp: Guid.NewGuid().ToString("N").ToUpperInvariant());

        using var response = await SendWithBearerAsync(factory, PingPath, token);

        await AssertSessionExpiredAsync(response);
    }

    [Fact]
    public async Task Should_Return401SessionExpired_When_StampDiffersOnlyByCase()
    {
        var user = await TestTokens.CreateForNewUserAsync(factory);
        var stamp = await StoredStampAsync(user.UserId);
        var token = TestTokens.Create(user.UserId, stamp: stamp.ToLowerInvariant());
        stamp.ShouldNotBe(stamp.ToLowerInvariant());

        using var response = await SendWithBearerAsync(factory, PingPath, token);

        await AssertSessionExpiredAsync(response);
    }

    [Fact]
    public async Task Should_Return401SessionExpired_When_UserDoesNotExist()
    {
        var token = TestTokens.Create(Guid.NewGuid(), stamp: Guid.NewGuid().ToString("N"));

        using var response = await SendWithBearerAsync(factory, PingPath, token);

        await AssertSessionExpiredAsync(response);
    }

    [Fact]
    public async Task Should_Return401SessionExpired_When_StoredStampEmpty()
    {
        var user = await TestTokens.CreateForNewUserAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
                .Where(row => row.Id == user.UserId)
                .ExecuteUpdateAsync(set => set.SetProperty(row => row.SecurityStamp, string.Empty), Ct);
        }

        using var response = await SendWithBearerAsync(factory, PingPath, user.Token);

        await AssertSessionExpiredAsync(response);
    }

    [Fact]
    public async Task Should_KeepGenericUnauthorizedBody_When_SignatureInvalid()
    {
        var user = await TestTokens.CreateForNewUserAsync(factory);
        var stamp = await StoredStampAsync(user.UserId);
        var token = TestTokens.Create(user.UserId, stamp: stamp, signingKey: "another-signing-key-0123456789-abcdef");

        using var response = await SendWithBearerAsync(factory, PingPath, token);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("title").GetString().ShouldBe("Unauthorized");
        problem.TryGetProperty("code", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Logout_When_AccessCookieStale()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        await AssertMeOkAsync(client);
        await BumpStampWithBearerLogoutAsync(client);
        var staleStamp = await StoredStampAsync(credentials);
        var refreshToken = client.Cookie("mita_rt", "/v1/auth/")!.Value;

        using (var logout = await client.LogoutAsync(ct: Ct))
        {
            logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await StoredStampAsync(credentials)).ShouldNotBe(staleStamp);
        using var stale = new AuthClient(factory);
        stale.SetRefreshCookie(refreshToken);
        using var refreshed = await stale.RefreshAsync(Ct);
        refreshed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_AllowLogin_When_AccessCookieStale()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        await BumpStampWithBearerLogoutAsync(client);

        using (var login = await client.LoginAsync(credentials, Ct))
        {
            login.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await AssertMeOkAsync(client);
    }

    private static async Task AssertMeOkAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync(MePath, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task AssertReplayRejectedAsync(WebApplicationFactory<Program> app, string accessToken)
    {
        using var bare = app.CreateDefaultClient(AuthClient.BaseAddress);

        using (var cookieRequest = new HttpRequestMessage(HttpMethod.Get, MePath))
        {
            cookieRequest.Headers.Add("Cookie", $"mita_at={accessToken}");
            using var cookieResponse = await bare.SendAsync(cookieRequest, Ct);
            await AssertSessionExpiredAsync(cookieResponse);
        }

        using var bearerResponse = await SendWithBearerAsync(app, MePath, accessToken);
        await AssertSessionExpiredAsync(bearerResponse);
    }

    private static async Task WaitUntilRejectedAsync(WebApplicationFactory<Program> app, string accessToken)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            using var response = await SendWithBearerAsync(app, MePath, accessToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), Ct);
        }
    }

    private static async Task<HttpResponseMessage> SendWithBearerAsync(
        WebApplicationFactory<Program> app,
        string path,
        string token)
    {
        using var bare = app.CreateDefaultClient(AuthClient.BaseAddress);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await bare.SendAsync(request, Ct);
    }

    private static async Task AssertSessionExpiredAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ShouldContain(challenge => challenge.Scheme == "Bearer");
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("status").GetInt32().ShouldBe(401);
        problem.GetProperty("title").GetString().ShouldBe("Unauthorized");
        problem.GetProperty("code").GetString().ShouldBe("auth.sessionExpired");
    }

    private static async Task<HttpResponseMessage> SendMeAsync(AuthClient client, HttpMethod method, object body)
    {
        using var request = client.WithXsrf(new HttpRequestMessage(method, MePath) { Content = JsonContent.Create(body) });
        return await client.Http.SendAsync(request, Ct);
    }

    private async Task BumpStampWithBearerLogoutAsync(AuthClient client)
    {
        using var other = new AuthClient(factory);
        using var response = await other.LogoutAsync(client.Cookie("mita_at")!.Value, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<string> StoredStampAsync(TestCredentials credentials)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var normalized = credentials.Email.ToUpperInvariant();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking()
            .Where(row => row.NormalizedEmail == normalized)
            .Select(row => row.SecurityStamp)
            .SingleAsync(Ct))!;
    }

    private async Task<Guid> UserIdAsync(TestCredentials credentials)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var normalized = credentials.Email.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking()
            .Where(row => row.NormalizedEmail == normalized)
            .Select(row => row.Id)
            .SingleAsync(Ct);
    }

    private async Task<string> StoredStampAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking()
            .Where(row => row.Id == userId)
            .Select(row => row.SecurityStamp)
            .SingleAsync(Ct))!;
    }
}
