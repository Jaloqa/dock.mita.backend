using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Wire;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Rates.GetRates;

public static class GetRatesEndpoint
{
    public static RouteGroupBuilder MapGetRates(this RouteGroupBuilder group)
    {
        group.MapGet(
                "/rates",
                async (string? date, string? from, string? to, TimeProvider time, IMediator mediator, CancellationToken ct) =>
                {
                    var effectiveDate = date ?? IsoDate.ToWire(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
                    return (await mediator.Send(new GetRatesQuery(effectiveDate, from, to), ct))
                        .ToHttp(rates => Results.Ok(rates));
                })
            .RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }
}
