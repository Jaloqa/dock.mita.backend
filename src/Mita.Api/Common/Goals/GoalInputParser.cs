using System.Text.Json;
using Mita.Api.Common.Wire;
using Mita.Domain.Goals;
using Mita.Domain.Periods;

namespace Mita.Api.Common.Goals;

public sealed record ParsedGoal(string Name, long Target, PeriodKey? Deadline, Guid? AccountId);

public static class GoalInputParser
{
    public static bool TryParseName(JsonElement? element, out string name)
    {
        name = string.Empty;
        if (JsonValues.IsMissing(element) || !JsonValues.TryGetOptionalText(element, out var text) || text is null)
        {
            return false;
        }

        name = text;
        return true;
    }

    public static ParsedGoal Parse(IGoalInput input)
    {
        if (!TryParseName(input.Name, out var name)
            || name.Length > Goal.NameMaxLength
            || !JsonValues.TryGetMinorAmount(input.Target, out var target))
        {
            throw new InvalidOperationException("The goal input reached the handler without passing validation.");
        }

        PeriodKey? deadline = null;
        if (!JsonValues.IsMissing(input.Deadline))
        {
            deadline = JsonValues.TryGetPeriodKey(input.Deadline, out var key)
                ? key
                : throw new InvalidOperationException("The goal input reached the handler without passing validation.");
        }

        Guid? accountId = null;
        if (!JsonValues.IsMissing(input.AccountId))
        {
            accountId = JsonValues.TryGetGuid(input.AccountId, out var id)
                ? id
                : throw new InvalidOperationException("The goal input reached the handler without passing validation.");
        }

        return new ParsedGoal(name, target, deadline, accountId);
    }
}
