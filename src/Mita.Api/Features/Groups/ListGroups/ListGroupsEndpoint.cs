using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.ListGroups;

public static class ListGroupsEndpoint
{
    public static RouteGroupBuilder MapListGroups(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/groups",
                async (IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListGroupsQuery(page, pageSize), ct)).ToHttp(groups => Results.Ok(groups)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
