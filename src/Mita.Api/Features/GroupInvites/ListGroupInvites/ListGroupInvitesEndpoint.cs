using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.GroupInvites.ListGroupInvites;

public static class ListGroupInvitesEndpoint
{
    public static RouteGroupBuilder MapListGroupInvites(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/group-invites",
                async (IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListGroupInvitesQuery(page, pageSize), ct))
                        .ToHttp(invites => Results.Ok(invites)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
