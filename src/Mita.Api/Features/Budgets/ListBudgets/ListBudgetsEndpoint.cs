using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Budgets.ListBudgets;

public static class ListBudgetsEndpoint
{
    public static RouteGroupBuilder MapListBudgets(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/budgets",
                async (IMediator mediator, CancellationToken ct, string? period = null, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListBudgetsQuery(period, page, pageSize), ct))
                        .ToHttp(budgets => Results.Ok(budgets)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
