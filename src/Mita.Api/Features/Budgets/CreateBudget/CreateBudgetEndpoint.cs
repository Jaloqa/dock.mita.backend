using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Budgets.CreateBudget;

public static class CreateBudgetEndpoint
{
    public static RouteGroupBuilder MapCreateBudget(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/budgets",
                async (CreateBudgetRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new CreateBudgetCommand(request?.CategoryId, request?.Period, request?.Limit), ct))
                        .ToHttp(budget => Results.Created($"/v1/budgets/{budget.Id}", budget)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
