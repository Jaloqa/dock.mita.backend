using Mita.Api.Extensions;
using Mita.Api.Features.Debts.CreateDebt;
using Mita.Api.Features.Debts.ListDebts;

namespace Mita.Api.Features.Debts;

public static class DebtsEndpoints
{
    public static IEndpointRouteBuilder MapDebtsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Debts")
            .MapListDebts()
            .MapCreateDebt();

        return app;
    }
}
