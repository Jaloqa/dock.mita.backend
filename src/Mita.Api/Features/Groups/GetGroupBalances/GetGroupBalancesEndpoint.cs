using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.GetGroupBalances;

public static class GetGroupBalancesEndpoint
{
    public static RouteGroupBuilder MapGetGroupBalances(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/groups/{id:guid}/balances",
                async (Guid id, IMediator mediator, CancellationToken ct, string? currency = null) =>
                    (await mediator.Send(new GetGroupBalancesQuery(GroupScope.User(id), currency), ct))
                        .ToHttp(balances => Results.Ok(balances)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedGetGroupBalances(this RouteGroupBuilder group)
    {
        group.MapGet(
            "/balances",
            async (string token, IMediator mediator, CancellationToken ct, string? currency = null) =>
                (await mediator.Send(new GetGroupBalancesQuery(GroupScope.Link(token, null), currency), ct))
                    .ToHttp(balances => Results.Ok(balances)));

        return group;
    }
}
