using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.ListOpenShares;

public static class ListOpenSharesEndpoint
{
    public static RouteGroupBuilder MapListOpenShares(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/groups/{id:guid}/open-shares",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new ListOpenSharesQuery(GroupScope.User(id)), ct))
                        .ToHttp(shares => Results.Ok(shares)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
