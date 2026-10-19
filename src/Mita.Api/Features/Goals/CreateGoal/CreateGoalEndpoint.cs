using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Goals.CreateGoal;

public static class CreateGoalEndpoint
{
    public static RouteGroupBuilder MapCreateGoal(this RouteGroupBuilder group)
    {
        group.MapPost(
                "/goals",
                async (CreateGoalRequest? request, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(
                        new CreateGoalCommand(request?.Name, request?.Target, request?.Deadline, request?.AccountId),
                        ct))
                        .ToHttp(goal => Results.Created($"/v1/goals/{goal.Id}", goal)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
