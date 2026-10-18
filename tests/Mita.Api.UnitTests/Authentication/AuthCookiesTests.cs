using Microsoft.AspNetCore.Http;
using Mita.Api.Authentication;
using Mita.Infrastructure.Authentication;
using Shouldly;

namespace Mita.Api.UnitTests.Authentication;

public sealed class AuthCookiesTests
{
    private static readonly IssuedTokens Tokens = new(
        "access",
        new DateTimeOffset(2026, 10, 18, 12, 15, 0, TimeSpan.Zero),
        "refresh",
        new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_WriteThreeCookiesWithDecidedAttributes(bool secure)
    {
        var context = new DefaultHttpContext();

        AuthCookies.Write(context.Response, Tokens, "xsrf", secure);

        var cookies = context.Response.Headers.SetCookie.Select(value => value!).ToList();
        var access = cookies.Single(cookie => cookie.StartsWith("mita_at=", StringComparison.Ordinal));
        var refresh = cookies.Single(cookie => cookie.StartsWith("mita_rt=", StringComparison.Ordinal));
        var xsrf = cookies.Single(cookie => cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));

        access.ShouldContain("httponly");
        access.ShouldContain("path=/;");
        (access.Contains("secure", StringComparison.Ordinal)).ShouldBe(secure);
        refresh.ShouldContain("httponly");
        refresh.ShouldContain("secure");
        refresh.ShouldContain("path=/v1/auth");
        xsrf.ShouldNotContain("httponly");
        xsrf.ShouldContain("samesite=lax");
        (xsrf.Contains("secure", StringComparison.Ordinal)).ShouldBe(secure);
    }

    [Fact]
    public void Should_ExpireAllThreeCookiesWithMatchingPaths_When_Clearing()
    {
        var context = new DefaultHttpContext();

        AuthCookies.Clear(context.Response);

        var cookies = context.Response.Headers.SetCookie.Select(value => value!).ToList();
        cookies.Count.ShouldBe(3);
        cookies.ShouldAllBe(cookie => cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.Ordinal));
        cookies.Single(cookie => cookie.StartsWith("mita_rt=", StringComparison.Ordinal)).ShouldContain("path=/v1/auth");
    }

    [Fact]
    public void Should_GenerateUnpredictableBase64UrlXsrfTokens()
    {
        var first = AuthCookies.NewXsrfToken();
        var second = AuthCookies.NewXsrfToken();

        first.ShouldNotBe(second);
        first.Length.ShouldBe(43);
        first.ShouldNotContain("=");
    }
}
