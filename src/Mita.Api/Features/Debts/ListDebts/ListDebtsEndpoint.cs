using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Debts.ListDebts;

public static class ListDebtsEndpoint
{
    public static RouteGroupBuilder MapListDebts(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/debts",
                async (IMediator mediator, CancellationToken ct, string? status = null, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListDebtsQuery(status, page, pageSize), ct)).ToHttp(debts => Results.Ok(debts)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
