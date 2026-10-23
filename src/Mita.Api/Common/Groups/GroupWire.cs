using Mita.Domain.Groups;

namespace Mita.Api.Common.Groups;

public static class GroupWire
{
    public static string ToWire(GroupSplitMode mode) => mode switch
    {
        GroupSplitMode.Equal => "equal",
        GroupSplitMode.Exact => "exact",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    public static string ToWire(GroupEntryAction action) => action switch
    {
        GroupEntryAction.Created => "created",
        GroupEntryAction.Updated => "updated",
        GroupEntryAction.Deleted => "deleted",
        GroupEntryAction.Restored => "restored",
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
    };

    public static string ToWire(GroupRole role) => role switch
    {
        GroupRole.Owner => "owner",
        GroupRole.Member => "member",
        GroupRole.Link => "link",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    public static string ToWireCategory(string? standardCategory, Guid? categoryId) =>
        standardCategory
        ?? categoryId?.ToString()
        ?? throw new ArgumentException("An expense has a standard category or a group category.", nameof(categoryId));

    public static bool TryParseDeletedFlag(string? value, out bool deleted)
    {
        (deleted, var ok) = value switch
        {
            null => (false, true),
            "true" => (true, true),
            "false" => (false, true),
            _ => (false, false)
        };
        return ok;
    }
}
