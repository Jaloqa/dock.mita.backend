using Mita.Api.Extensions;
using Mita.Api.Features.Groups.AddCategory;
using Mita.Api.Features.Groups.CreateExpense;
using Mita.Api.Features.Groups.CreateGroupRepayment;
using Mita.Api.Features.Groups.DeleteExpense;
using Mita.Api.Features.Groups.DeleteGroupRepayment;
using Mita.Api.Features.Groups.ExpenseHistory;
using Mita.Api.Features.Groups.GetGroup;
using Mita.Api.Features.Groups.GetGroupBalances;
using Mita.Api.Features.Groups.GroupRepaymentHistory;
using Mita.Api.Features.Groups.ListExpenses;
using Mita.Api.Features.Groups.ListGroupRepayments;
using Mita.Api.Features.Groups.RestoreExpense;
using Mita.Api.Features.Groups.RestoreGroupRepayment;
using Mita.Api.Features.Groups.UpdateExpense;
using Mita.Api.Features.Groups.UpdateGroupRepayment;
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
            .MapSharedAddCategory()
            .MapSharedGetGroupBalances()
            .MapSharedListExpenses()
            .MapSharedCreateExpense()
            .MapSharedUpdateExpense()
            .MapSharedDeleteExpense()
            .MapSharedRestoreExpense()
            .MapSharedExpenseHistory()
            .MapSharedListGroupRepayments()
            .MapSharedCreateGroupRepayment()
            .MapSharedUpdateGroupRepayment()
            .MapSharedDeleteGroupRepayment()
            .MapSharedRestoreGroupRepayment()
            .MapSharedGroupRepaymentHistory()
            .MapSharedGetRates();

        return app;
    }
}
