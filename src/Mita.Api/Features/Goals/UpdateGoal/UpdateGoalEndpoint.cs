using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Goals.UpdateGoal;

public static class UpdateGoalEndpoint
{
    public static RouteGroupBuilder MapUpdateGoal(this RouteGroupBuilder group)
    {
        group.MapPut(
                "/goals/{id:guid}",
                async (Guid id, UpdateGoalRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(
                        new UpdateGoalCommand(id, request?.Name, request?.Target, request?.Deadline, request?.AccountId),
                        ct))
                        .ToHttp(goal => Results.Ok(goal)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
