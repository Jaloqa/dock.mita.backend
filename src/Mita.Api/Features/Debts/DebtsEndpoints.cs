using Mita.Api.Extensions;
using Mita.Api.Features.Debts.AddRepayment;
using Mita.Api.Features.Debts.CreateDebt;
using Mita.Api.Features.Debts.DeleteDebt;
using Mita.Api.Features.Debts.DeleteRepayment;
using Mita.Api.Features.Debts.ListDebts;
using Mita.Api.Features.Debts.ListGroupDebts;
using Mita.Api.Features.Debts.ListRepayments;
using Mita.Api.Features.Debts.UpdateDebt;

namespace Mita.Api.Features.Debts;

public static class DebtsEndpoints
{
    public static IEndpointRouteBuilder MapDebtsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Debts")
            .MapListDebts()
            .MapListGroupDebts()
            .MapCreateDebt()
            .MapUpdateDebt()
            .MapDeleteDebt()
            .MapAddRepayment()
            .MapListRepayments()
            .MapDeleteRepayment();

        return app;
    }
}
