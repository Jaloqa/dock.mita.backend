using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Goals.DeleteGoal;

public static class DeleteGoalEndpoint
{
    public static RouteGroupBuilder MapDeleteGoal(this RouteGroupBuilder group)
    {
        group.MapDelete(
                "/goals/{id:guid}",
                async (Guid id, IMediator mediator, CancellationToken ct) =>
                    (await mediator.Send(new DeleteGoalCommand(id), ct)).ToHttp(() => Results.NoContent()))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
