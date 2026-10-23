using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Debts.ListGroupDebts;

public static class ListGroupDebtsEndpoint
{
    public static RouteGroupBuilder MapListGroupDebts(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/debts/groups",
                async (IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListGroupDebtsQuery(page, pageSize), ct))
                        .ToHttp(lines => Results.Ok(lines)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
