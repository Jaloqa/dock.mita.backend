using Mita.Api.Extensions;
using Mita.Api.Features.Rates.GetRates;

namespace Mita.Api.Features.Rates;

public static class RatesEndpoints
{
    public static IEndpointRouteBuilder MapRatesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Rates")
            .MapGetRates();

        return app;
    }
}
