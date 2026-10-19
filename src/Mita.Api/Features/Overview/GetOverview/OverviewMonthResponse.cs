namespace Mita.Api.Features.Overview.GetOverview;

public sealed record OverviewMonthResponse(
    string Period,
    string From,
    string To,
    long Planned,
    long Spent,
    long Left,
    int Budgets);
