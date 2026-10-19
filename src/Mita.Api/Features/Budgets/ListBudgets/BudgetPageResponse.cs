using Mita.Api.Common.Budgets;

namespace Mita.Api.Features.Budgets.ListBudgets;

public sealed record BudgetPageResponse(
    IReadOnlyList<BudgetDto> Items,
    int Page,
    int PageSize,
    int Total,
    string Period,
    string From,
    string To,
    string Currency,
    BudgetSummary Summary);
