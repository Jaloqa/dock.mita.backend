using System.Text.Json;
using Mita.Api.Common.Wire;
using Mita.Domain.Groups;

namespace Mita.Api.Common.Groups;

public sealed record ParsedSplit(GroupSplitMode Mode, IReadOnlyList<Guid> MemberIds, IReadOnlyList<long> Amounts);

public static class GroupSplitInput
{
    public const string UnknownMember = "validation.unknownMember";
    public const string SplitMismatch = "validation.splitMismatch";

    private const string InvalidValue = "validation.invalidValue";

    public static string? Error(JsonElement? split, JsonElement? amount)
    {
        var error = Read(split, out var parsed);
        if (error is not null || parsed is not { Mode: GroupSplitMode.Exact })
        {
            return error;
        }

        return JsonValues.TryGetMinorAmount(amount, out var total) && total > 0 && parsed.Amounts.Sum() != total
            ? SplitMismatch
            : null;
    }

    public static ParsedSplit? Parse(JsonElement? split) =>
        Read(split, out var parsed) is null
            ? parsed
            : throw new InvalidOperationException("The split reached the handler without passing validation.");

    private static string? Read(JsonElement? split, out ParsedSplit? parsed)
    {
        parsed = null;
        if (split is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
        {
            return null;
        }

        if (split is not { ValueKind: JsonValueKind.Object } value
            || !value.TryGetProperty("mode", out var mode)
            || mode.ValueKind != JsonValueKind.String)
        {
            return InvalidValue;
        }

        return mode.GetString() switch
        {
            "equal" => ReadEqual(value, out parsed),
            "exact" => ReadExact(value, out parsed),
            _ => InvalidValue
        };
    }

    private static string? ReadEqual(JsonElement split, out ParsedSplit? parsed)
    {
        parsed = null;
        if (!TryGetItems(split, "memberIds", out var items))
        {
            return InvalidValue;
        }

        var error = ReadMemberIds(items.Select(item => (JsonElement?)item), out var memberIds);
        if (error is null)
        {
            parsed = new ParsedSplit(GroupSplitMode.Equal, memberIds, []);
        }

        return error;
    }

    private static string? ReadExact(JsonElement split, out ParsedSplit? parsed)
    {
        parsed = null;
        if (!TryGetItems(split, "shares", out var items))
        {
            return InvalidValue;
        }

        var amounts = new List<long>();
        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("amount", out var amount)
                || !JsonValues.TryGetMinorAmount(amount, out var value)
                || value <= 0)
            {
                return InvalidValue;
            }

            amounts.Add(value);
        }

        var error = ReadMemberIds(
            items.Select(item => item.TryGetProperty("memberId", out var memberId) ? memberId : (JsonElement?)null),
            out var memberIds);
        if (error is null)
        {
            parsed = new ParsedSplit(GroupSplitMode.Exact, memberIds, amounts);
        }

        return error;
    }

    private static string? ReadMemberIds(IEnumerable<JsonElement?> items, out List<Guid> memberIds)
    {
        memberIds = [];
        var seen = new HashSet<(JsonValueKind Kind, string Key)>();
        var repeated = false;
        var unknown = false;
        foreach (var item in items)
        {
            if (JsonValues.TryGetGuid(item, out var memberId))
            {
                memberIds.Add(memberId);
                repeated |= !seen.Add((JsonValueKind.String, memberId.ToString()));
                continue;
            }

            unknown = true;
            if (item is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } value)
            {
                var key = value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : value.GetRawText();
                repeated |= !seen.Add((value.ValueKind, key));
            }
        }

        return repeated ? InvalidValue : unknown ? UnknownMember : null;
    }

    private static bool TryGetItems(JsonElement split, string field, out List<JsonElement> items)
    {
        items = split.TryGetProperty(field, out var array) && array.ValueKind == JsonValueKind.Array
            ? [.. array.EnumerateArray()]
            : [];
        return items.Count is > 0 and <= Group.MaxMembers;
    }
}
