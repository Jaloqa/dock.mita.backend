using Mita.Api.Common.Goals;

namespace Mita.Api.Features.Goals.ListGoals;

public sealed record GoalPageResponse(
    IReadOnlyList<GoalDto> Items,
    int Page,
    int PageSize,
    int Total,
    string Currency,
    GoalSummary Summary);
