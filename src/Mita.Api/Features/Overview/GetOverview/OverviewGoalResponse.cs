namespace Mita.Api.Features.Overview.GetOverview;

public sealed record OverviewGoalResponse(
    Guid Id,
    string Name,
    long Target,
    long Saved,
    int Percent,
    string Status,
    string? Deadline);
