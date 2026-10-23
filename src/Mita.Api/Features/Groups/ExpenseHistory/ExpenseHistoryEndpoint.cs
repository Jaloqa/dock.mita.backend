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
                    (await mediator.Send(new ExpenseHistoryQuery(GroupScope.User(id), expenseId, page, pageSize), ct))
                        .ToHttp(history => Results.Ok(history)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedExpenseHistory(this RouteGroupBuilder group)
    {
        group.MapGet(
            "/expenses/{expenseId:guid}/history",
            async (string token, Guid expenseId, IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 100) =>
                (await mediator.Send(new ExpenseHistoryQuery(GroupScope.Link(token, null), expenseId, page, pageSize), ct))
                    .ToHttp(history => Results.Ok(history)));

        return group;
    }
}
