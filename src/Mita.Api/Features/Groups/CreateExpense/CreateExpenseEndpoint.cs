using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Groups.CreateExpense;

public static class CreateExpenseEndpoint
{
    public static RouteGroupBuilder MapCreateExpense(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/groups/{id:guid}/expenses",
                async (Guid id, CreateExpenseRequest? request, IMediator mediator, CancellationToken ct) =>
                {
                    var command = new CreateExpenseCommand(
                        new GroupScope(id),
                        request?.PayerId,
                        request?.Amount,
                        request?.Currency,
                        request?.Date,
                        request?.CategoryId,
                        request?.Tags,
                        request?.Description,
                        request?.Rate,
                        request?.Split);

                    return (await mediator.Send(command, ct))
                        .ToHttp(expense => Results.Created((string?)null, expense));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
