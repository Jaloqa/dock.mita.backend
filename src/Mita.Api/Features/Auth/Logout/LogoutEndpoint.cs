using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Auth.Logout;

public static class LogoutEndpoint
{
    public static RouteGroupBuilder MapLogout(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/logout",
                async (HttpContext http, IMediator mediator, CancellationToken ct) =>
                {
                    var refreshToken = http.Request.Cookies[AuthCookies.RefreshToken];
                    Guid? userId = CurrentUser.TryReadId(http.User, out var id) ? id : null;

                    http.Response.OnStarting(() =>
                    {
                        AuthCookies.Clear(http.Response);
                        return Task.CompletedTask;
                    });

                    var result = await mediator.Send(new LogoutCommand(refreshToken, userId), ct);

                    return result.ToHttp(() => Results.NoContent());
                })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.SessionPolicy);

        return group;
    }
}
