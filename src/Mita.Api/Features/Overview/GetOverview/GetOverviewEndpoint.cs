using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Overview.GetOverview;

public static class GetOverviewEndpoint
{
    public static RouteGroupBuilder MapGetOverview(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/overview",
                async (IMediator mediator, CancellationToken ct, string? period = null) =>
                    (await mediator.Send(new GetOverviewQuery(period), ct))
                        .ToHttp(overview => Results.Ok(overview)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
