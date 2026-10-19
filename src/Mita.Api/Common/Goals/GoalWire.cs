using Mita.Domain.Goals;

namespace Mita.Api.Common.Goals;

public static class GoalWire
{
    public static string ToWire(GoalStatus status) => status switch
    {
        GoalStatus.Active => "active",
        GoalStatus.Reached => "reached",
        GoalStatus.Overdue => "overdue",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
