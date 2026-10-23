using Mita.Api.Extensions;
using Mita.Api.Features.Groups.ExpenseHistory;
using Mita.Api.Features.Groups.GetGroup;
using Mita.Api.Features.Groups.GetGroupBalances;
using Mita.Api.Features.Groups.GroupRepaymentHistory;
using Mita.Api.Features.Groups.ListExpenses;
using Mita.Api.Features.Groups.ListGroupRepayments;
using Mita.Api.Features.Rates.GetRates;

namespace Mita.Api.Features.Groups;

public static class SharedGroupsEndpoints
{
    public static IEndpointRouteBuilder MapSharedGroupsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapVersioned()
            .MapGroup("/shared/groups/{token}")
            .WithTags("Shared groups")
            .RequireAuthorization(AuthExtensions.GroupLinkPolicy)
            .RequireRateLimiting(RateLimitingExtensions.GroupLinkPolicy)
            .MapSharedGetGroup()
            .MapSharedGetGroupBalances()
            .MapSharedListExpenses()
            .MapSharedExpenseHistory()
            .MapSharedListGroupRepayments()
            .MapSharedGroupRepaymentHistory()
            .MapSharedGetRates();

        return app;
    }
}
