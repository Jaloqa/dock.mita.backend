using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Auth.Refresh;

public static class RefreshEndpoint
{
    public static RouteGroupBuilder MapRefresh(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/refresh",
                async (HttpContext http, IMediator mediator, IHostEnvironment env, CancellationToken ct) =>
                {
                    var refreshToken = http.Request.Cookies[AuthCookies.RefreshToken];
                    var result = await mediator.Send(new RefreshCommand(refreshToken), ct);

                    if (!result.IsSuccess)
                    {
                        AuthCookies.Clear(http.Response);
                    }

                    return result.ToHttp(response =>
                    {
                        if (response.Tokens is not null)
                        {
                            AuthCookies.Write(http.Response, response.Tokens, AuthCookies.NewXsrfToken(), !env.IsDevelopment());
                        }

                        return Results.NoContent();
                    });
                })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.SessionPolicy);

        return group;
    }
}
