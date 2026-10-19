using Mita.Api.Extensions;
using Mita.Api.Features.Budgets.CreateBudget;
using Mita.Api.Features.Budgets.DeleteBudget;
using Mita.Api.Features.Budgets.ListBudgets;
using Mita.Api.Features.Budgets.UpdateBudget;

namespace Mita.Api.Features.Budgets;

public static class BudgetsEndpoints
{
    public static IEndpointRouteBuilder MapBudgetsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapV1()
            .WithTags("Budgets")
            .MapListBudgets()
            .MapCreateBudget()
            .MapUpdateBudget()
            .MapDeleteBudget();

        return app;
    }
}