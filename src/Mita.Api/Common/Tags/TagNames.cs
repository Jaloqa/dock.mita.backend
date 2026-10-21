using System.Text.Json;
using Mita.Api.Common.Wire;
using Mita.Domain.Tags;

namespace Mita.Api.Common.Tags;

public static class TagNames
{
    public const string Field = "tags";
    public const string InvalidValue = "validation.invalidValue";
    public const string TagTooLong = "validation.tagTooLong";
    public const string TooManyTags = "validation.tooManyTags";

    private const string Required = "validation.required";

    public static string? Error(JsonElement? element)
    {
        if (element is null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
        {
            return null;
        }

        if (element is not { ValueKind: JsonValueKind.Array } array)
        {
            return InvalidValue;
        }

        var names = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (!JsonValues.TryGetString(item, out var text))
            {
                return InvalidValue;
            }

            var trimmed = text.Trim();
            if (trimmed.Length == 0 || trimmed.Any(char.IsControl))
            {
                return InvalidValue;
            }

            names.Add(trimmed);
        }

        if (names.Any(name => name.Length > Tag.NameMaxLength))
        {
            return TagTooLong;
        }

        return Distinct(names).Count > Tag.MaxPerTransaction ? TooManyTags : null;
    }

    public static IReadOnlyList<string> Parse(JsonElement? element)
    {
        if (Error(element) is not null)
        {
            throw new InvalidOperationException("The tags reached the handler without passing validation.");
        }

        if (element is not { ValueKind: JsonValueKind.Array } array)
        {
            return [];
        }

        return Distinct(array.EnumerateArray().Select(item => item.GetString()!.Trim()));
    }

    public static string? NameError(JsonElement? element)
    {
        if (JsonValues.IsMissing(element))
        {
            return Required;
        }

        if (!JsonValues.TryGetString(element, out var text))
        {
            return InvalidValue;
        }

        var trimmed = text.Trim();
        if (trimmed.Any(char.IsControl))
        {
            return InvalidValue;
        }

        return trimmed.Length > Tag.NameMaxLength ? TagTooLong : null;
    }

    public static bool TryParseName(JsonElement? element, out string name)
    {
        name = string.Empty;
        if (NameError(element) is not null)
        {
            return false;
        }

        JsonValues.TryGetString(element, out var text);
        name = text.Trim();
        return true;
    }

    private static List<string> Distinct(IEnumerable<string> names)
    {
        var seen = new HashSet<string>();
        return names.Where(name => seen.Add(name.ToLowerInvariant())).ToList();
    }
}
