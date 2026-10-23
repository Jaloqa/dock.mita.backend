using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.DeleteExpense;

public static class DeleteExpenseEndpoint
{
    public static RouteGroupBuilder MapDeleteExpense(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/groups/{id:guid}/expenses/{expenseId:guid}",
                async (Guid id, Guid expenseId, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new DeleteExpenseCommand(new GroupScope(id), expenseId), ct))
                        .ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
