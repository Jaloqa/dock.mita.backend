using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.RestoreExpense;

public static class RestoreExpenseEndpoint
{
    public static RouteGroupBuilder MapRestoreExpense(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/groups/{id:guid}/expenses/{expenseId:guid}/restore",
                async (Guid id, Guid expenseId, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new RestoreExpenseCommand(new GroupScope(id), expenseId), ct))
                        .ToHttp(expense => Results.Ok(expense)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
