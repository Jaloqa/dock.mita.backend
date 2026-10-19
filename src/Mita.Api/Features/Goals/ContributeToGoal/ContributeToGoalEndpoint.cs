using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Goals.ContributeToGoal;

public static class ContributeToGoalEndpoint
{
    public static RouteGroupBuilder MapContributeToGoal(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/goals/{id:guid}/contributions",
                async (Guid id, ContributeToGoalRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(
                        new ContributeToGoalCommand(id, request?.Amount, request?.Date, request?.FromAccountId, request?.Rate),
                        ct))
                        .ToHttp(goal => Results.Ok(goal)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
