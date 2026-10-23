using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.ExpenseHistory;

public static class ExpenseHistoryEndpoint
{
    public static RouteGroupBuilder MapExpenseHistory(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/groups/{id:guid}/expenses/{expenseId:guid}/history",
                async (Guid id, Guid expenseId, IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ExpenseHistoryQuery(new GroupScope(id), expenseId, page, pageSize), ct))
                        .ToHttp(history => Results.Ok(history)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
