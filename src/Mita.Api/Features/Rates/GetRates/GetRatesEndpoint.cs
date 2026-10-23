using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Wire;
using Mita.Api.Extensions;

namespace Mita.Api.Features.Rates.GetRates;

public static class GetRatesEndpoint
{
    public static RouteGroupBuilder MapGetRates(this RouteGroupBuilder group)
    {
        group.MapGet("/rates", GetAsync).RequireAuthorization(AuthExtensions.UserPolicy);

        return group;
    }

    public static RouteGroupBuilder MapSharedGetRates(this RouteGroupBuilder group)
    {
        group.MapGet(
            "/rates",
            (string token, string? date, string? from, string? to, TimeProvider time, IMediator mediator, CancellationToken ct) =>
                GetAsync(date, from, to, time, mediator, ct));

        return group;
    }

    private static async Task<IResult> GetAsync(
        string? date,
        string? from,
        string? to,
        TimeProvider time,
        IMediator mediator,
        CancellationToken ct)
    {
        var today = IsoDate.ToWire(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
        return (await mediator.Send(new GetRatesQuery(date ?? today, from, to, today), ct))
            .ToHttp(rates => Results.Ok(rates));
    }
}
