using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Budgets.UpdateBudget;

public static class UpdateBudgetEndpoint
{
    public static RouteGroupBuilder MapUpdateBudget(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/budgets/{id:guid}",
                async (Guid id, UpdateBudgetRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new UpdateBudgetCommand(id, request?.CategoryId, request?.Period, request?.Limit), ct))
                        .ToHttp(budget => Results.Ok(budget)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
