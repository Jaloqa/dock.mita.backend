using Mita.Api.Extensions;
using Mita.Api.Features.Budgets.ListBudgets;

namespace Mita.Api.Features.Budgets;

public static class BudgetsEndpoints
{
    public static IEndpointRouteBuilder MapBudgetsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Budgets")
            .MapListBudgets();

        return app;
    }
}
