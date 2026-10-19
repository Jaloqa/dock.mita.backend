namespace Mita.Api.Features.Overview.GetOverview;

public sealed record OverviewBudgetResponse(
    Guid Id,
    Guid CategoryId,
    string Name,
    long Limit,
    long Spent,
    long Remaining,
    string Status);
