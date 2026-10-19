using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Goals.ListGoals;

public static class ListGoalsEndpoint
{
    public static RouteGroupBuilder MapListGoals(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/goals",
                async (IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 100) =>
                    (await mediator.Send(new ListGoalsQuery(page, pageSize), ct)).ToHttp(goals => Results.Ok(goals)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
