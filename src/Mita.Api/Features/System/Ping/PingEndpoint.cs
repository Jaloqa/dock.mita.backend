using Mita.Api.Authentication;
using Mita.Api.Extensions;

namespace Mita.Api.Features.System.Ping;

public static class PingEndpoint
{
    public static RouteGroupBuilder MapPing(this RouteGroupBuilder group)
    {
        group.MapGet("/ping", (ICurrentUser currentUser) => TypedResults.Ok(new PingResponse(currentUser.Id)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
