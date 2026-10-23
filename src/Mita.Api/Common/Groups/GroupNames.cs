using System.Text.Json;
using Mita.Api.Common.Wire;
using Mita.Domain.Groups;

namespace Mita.Api.Common.Groups;

public static class GroupNames
{
    public const string Required = "validation.required";
    public const string InvalidValue = "validation.invalidValue";
    public const string NameTooLong = "validation.nameTooLong";

    public static string? Error(JsonElement? value)
    {
        if (JsonValues.IsMissing(value))
        {
            return Required;
        }

        if (!JsonValues.TryGetString(value, out var text))
        {
            return InvalidValue;
        }

        var trimmed = text.Trim();
        if (trimmed.Any(char.IsControl))
        {
            return InvalidValue;
        }

        return trimmed.Length > Group.NameMaxLength ? NameTooLong : null;
    }

    public static string Parse(JsonElement? value)
    {
        if (Error(value) is not null || !JsonValues.TryGetString(value, out var text))
        {
            throw new InvalidOperationException("The name reached the handler without passing validation.");
        }

        return text.Trim();
    }
}
