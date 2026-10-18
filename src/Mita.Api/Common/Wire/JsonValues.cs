using System.Text.Json;
using Mita.Domain.Money;

namespace Mita.Api.Common.Wire;

public static class JsonValues
{
    public static bool IsMissing(JsonElement? element) => element switch
    {
        null => true,
        { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => true,
        { ValueKind: JsonValueKind.String } text => string.IsNullOrWhiteSpace(text.GetString()),
        _ => false
    };

    public static bool TryGetString(JsonElement? element, out string value)
    {
        value = string.Empty;
        if (element is not { ValueKind: JsonValueKind.String } text)
        {
            return false;
        }

        value = text.GetString() ?? string.Empty;
        return true;
    }

    public static bool TryGetMinorAmount(JsonElement? element, out long amount)
    {
        amount = 0;
        if (element is not { ValueKind: JsonValueKind.Number } number
            || !number.TryGetDouble(out var value)
            || value != Math.Floor(value)
            || Math.Abs(value) > MinorUnits.MaxAmount)
        {
            return false;
        }

        amount = (long)value;
        return true;
    }

    public static bool TryGetBoolean(JsonElement? element, out bool value)
    {
        value = false;
        if (element is not { ValueKind: JsonValueKind.True or JsonValueKind.False } flag)
        {
            return false;
        }

        value = flag.GetBoolean();
        return true;
    }
}
