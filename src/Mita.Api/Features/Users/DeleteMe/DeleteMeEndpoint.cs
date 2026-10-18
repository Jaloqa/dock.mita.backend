using MediatR;
using Microsoft.AspNetCore.Mvc;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Users.DeleteMe;

public static class DeleteMeEndpoint
{
    public static RouteGroupBuilder MapDeleteMe(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/me",
                async ([FromBody] DeleteMeRequest? request, HttpContext http, IMediator mediator, CancellationToken ct) =>
                {
                    var result = await mediator.Send(new DeleteMeCommand(request?.Password), ct);

                    return result.ToHttp(() =>
                    {
                        AuthCookies.Clear(http.Response);
                        return Results.NoContent();
                    });
                })
            .RequireAuthorization(AuthExtensions.UserPolicy)
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        return group;
    }
}
