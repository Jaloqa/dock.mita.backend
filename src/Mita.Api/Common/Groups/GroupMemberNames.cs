using System.Text.Json;
using Mita.Api.Common.Wire;
using Mita.Domain.Groups;

namespace Mita.Api.Common.Groups;

public static class GroupMemberNames
{
    public const string TooManyMembers = "validation.tooManyMembers";
    public const string MemberNameTaken = "validation.memberNameTaken";

    public static string? Error(JsonElement? members)
    {
        if (JsonValues.IsMissing(members))
        {
            return GroupNames.Required;
        }

        if (members is not { ValueKind: JsonValueKind.Array } array)
        {
            return GroupNames.InvalidValue;
        }

        if (array.GetArrayLength() == 0)
        {
            return GroupNames.Required;
        }

        var names = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (!JsonValues.TryGetString(item, out var text))
            {
                return GroupNames.InvalidValue;
            }

            var trimmed = text.Trim();
            if (trimmed.Length == 0 || trimmed.Any(char.IsControl))
            {
                return GroupNames.InvalidValue;
            }

            names.Add(trimmed);
        }

        if (names.Any(name => name.Length > Group.NameMaxLength))
        {
            return GroupNames.NameTooLong;
        }

        if (names.Count > Group.MaxMembers)
        {
            return TooManyMembers;
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Count ? null : MemberNameTaken;
    }

    public static IReadOnlyList<string> Parse(JsonElement? members)
    {
        if (Error(members) is not null || members is not { ValueKind: JsonValueKind.Array } array)
        {
            throw new InvalidOperationException("The members reached the handler without passing validation.");
        }

        return [.. array.EnumerateArray().Select(item => item.GetString()!.Trim())];
    }
}
