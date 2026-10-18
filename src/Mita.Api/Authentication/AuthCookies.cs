using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Mita.Infrastructure.Authentication;

namespace Mita.Api.Authentication;

public static class AuthCookies
{
    public const string AccessToken = "mita_at";
    public const string RefreshToken = "mita_rt";
    public const string XsrfToken = "XSRF-TOKEN";
    public const string XsrfHeader = "X-XSRF-TOKEN";
    public const string RefreshPath = "/v1/auth";

    private const int XsrfTokenBytes = 32;

    public static string NewXsrfToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(XsrfTokenBytes));

    public static void Write(HttpResponse response, IssuedTokens tokens, string xsrfToken, bool secure)
    {
        response.Cookies.Append(AccessToken, tokens.AccessToken, Options(true, secure, "/", tokens.AccessExpires));
        response.Cookies.Append(RefreshToken, tokens.RefreshToken, Options(true, true, RefreshPath, tokens.RefreshExpires));
        response.Cookies.Append(XsrfToken, xsrfToken, Options(false, secure, "/", tokens.RefreshExpires));
    }

    public static void Clear(HttpResponse response)
    {
        var secure = response.HttpContext.Request.IsHttps;
        response.Cookies.Delete(AccessToken, Options(true, secure, "/", null));
        response.Cookies.Delete(RefreshToken, Options(true, true, RefreshPath, null));
        response.Cookies.Delete(XsrfToken, Options(false, secure, "/", null));
    }

    private static CookieOptions Options(bool httpOnly, bool secure, string path, DateTimeOffset? expires) => new()
    {
        HttpOnly = httpOnly,
        Secure = secure,
        SameSite = SameSiteMode.Lax,
        Path = path,
        Expires = expires
    };
}
