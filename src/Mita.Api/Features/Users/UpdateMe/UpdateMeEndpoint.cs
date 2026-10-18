using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Users.UpdateMe;

public static class UpdateMeEndpoint
{
    public static RouteGroupBuilder MapUpdateMe(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/me",
                async (UpdateMeRequest? request, HttpContext http, IMediator mediator, IHostEnvironment env, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new UpdateMeCommand(request?.Name, request?.Email), ct);

                    return result.ToHttp(response =>
                    {
                        if (response.Tokens is not null)
                        {
                            AuthCookies.Write(http.Response, response.Tokens, AuthCookies.NewXsrfToken(), !env.IsDevelopment());
                        }

                        return Results.Ok(response.User);
                    });
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
