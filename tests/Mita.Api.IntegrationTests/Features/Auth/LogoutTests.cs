using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Auth;

public sealed class LogoutTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_ClearCookies_When_LoggingOut()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await client.LogoutAsync(ct: Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        RefreshTests.AssertCookiesCleared(response);
        client.Cookie("mita_at").ShouldBeNull();
        client.Cookie("mita_rt", "/v1/auth/").ShouldBeNull();
        client.Cookie("XSRF-TOKEN").ShouldBeNull();
    }

    [Fact]
    public async Task Should_RejectRefresh_When_LoggedOut()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var refresh = client.Cookie("mita_rt", "/v1/auth/")!.Value;
        using (var response = await client.LogoutAsync(ct: Ct))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using var stale = new AuthClient(factory);
        stale.SetRefreshCookie(refresh);
        using var refreshed = await stale.RefreshAsync(Ct);

        await RefreshTests.AssertSessionExpiredAsync(refreshed);
    }

    [Fact]
    public async Task Should_Return204_When_NoCookie()
    {
        using var client = new AuthClient(factory);

        using var response = await client.LogoutAsync(ct: Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        RefreshTests.AssertCookiesCleared(response);
    }

    [Fact]
    public async Task Should_Return204_When_RefreshCookieUnknown()
    {
        using var client = new AuthClient(factory);
        client.SetRefreshCookie("not-a-real-token");

        using var response = await client.LogoutAsync(ct: Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Should_BumpSecurityStamp_When_BearerAuthenticated()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var before = await SecurityStampAsync(credentials);
        var bearer = client.Cookie("mita_at")!.Value;

        using var bare = new AuthClient(factory);
        using var response = await bare.LogoutAsync(bearer, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await SecurityStampAsync(credentials)).ShouldNotBe(before);
    }

    [Fact]
    public async Task Should_BumpSecurityStamp_When_OnlyRefreshCookiePresented()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var before = await SecurityStampAsync(credentials);

        using var response = await client.LogoutAsync(ct: Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await SecurityStampAsync(credentials)).ShouldNotBe(before);
    }

    private async Task<string?> SecurityStampAsync(TestCredentials credentials)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var normalized = credentials.Email.ToUpperInvariant();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking()
            .Where(row => row.NormalizedEmail == normalized)
            .Select(row => row.SecurityStamp)
            .SingleAsync(Ct);
    }
}
