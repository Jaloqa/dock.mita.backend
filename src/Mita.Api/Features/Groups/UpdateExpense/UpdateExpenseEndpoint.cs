using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.UpdateExpense;

public static class UpdateExpenseEndpoint
{
    public static RouteGroupBuilder MapUpdateExpense(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/groups/{id:guid}/expenses/{expenseId:guid}",
                async (Guid id, Guid expenseId, UpdateExpenseRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new UpdateExpenseCommand(
                        new GroupScope(id),
                        expenseId,
                        request?.PayerId,
                        request?.Amount,
                        request?.Currency,
                        request?.Date,
                        request?.CategoryId,
                        request?.Tags,
                        request?.Description,
                        request?.Rate,
                        request?.Split);

                    return (await mediator.Send(command, ct)).ToHttp(expense => Results.Ok(expense));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
