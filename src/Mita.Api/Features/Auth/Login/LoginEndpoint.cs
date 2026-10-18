using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;

namespace Mita.Api.Features.Auth.Login;

public static class LoginEndpoint
{
    public static RouteGroupBuilder MapLogin(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/login",
                async (LoginRequest? request, HttpContext http, IMediator mediator, IHostEnvironment env, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new LoginCommand(request?.Email, request?.Password), ct);

                    return result.ToHttp(response =>
                    {
                        AuthCookies.Write(http.Response, response.Tokens, AuthCookies.NewXsrfToken(), !env.IsDevelopment());
                        return Results.Ok(response.User);
                    });
                })
            .AllowAnonymous();

        return group;
    }
}
