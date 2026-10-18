using System.Security.Cryptography;
using System.Text;
using Mita.Api.Common;

namespace Mita.Api.Authentication;

public sealed class SkipXsrfMetadata;

public sealed class XsrfMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext http)
    {
        if (!RequiresCheck(http) || IsValid(http.Request))
        {
            return next(http);
        }

        return new ErrorProblemResult(Error.Forbidden("auth.xsrf")).ExecuteAsync(http);
    }

    private static bool RequiresCheck(HttpContext http) =>
        !IsSafeMethod(http.Request.Method)
        && http.IsCookieAuthenticated()
        && http.GetEndpoint()?.Metadata.GetMetadata<SkipXsrfMetadata>() is null;

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method)
        || HttpMethods.IsTrace(method);

    private static bool IsValid(HttpRequest request)
    {
        var header = request.Headers[AuthCookies.XsrfHeader].ToString();
        var cookie = request.Cookies[AuthCookies.XsrfToken];

        if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(cookie))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(header), Encoding.UTF8.GetBytes(cookie));
    }
}
