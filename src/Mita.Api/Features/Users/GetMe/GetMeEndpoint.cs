using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Users.GetMe;

public static class GetMeEndpoint
{
    public static RouteGroupBuilder MapGetMe(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/me",
                async (IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new GetMeQuery(), ct)).ToHttp(user => Results.Ok(user)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
