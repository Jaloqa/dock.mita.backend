using MediatR;
using Mita.Api.Common;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Reports.GetReport;

public static class GetReportEndpoint
{
    public static RouteGroupBuilder MapGetReport(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/reports",
                async (IMediator mediator, CancellationToken ct, string? range = null) =>
                    (await mediator.Send(new GetReportQuery(range), ct))
                        .ToHttp(report => Results.Ok(report)))
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
