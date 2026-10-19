using System.Text.Json;
using Mita.Domain.Money;
using Mita.Domain.Periods;

namespace Mita.Api.Common.Wire;

public static class JsonValues
{
    private const double MaxSafeInteger = 9_007_199_254_740_991;

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

    public static bool TryGetIsoDate(JsonElement? element, out DateOnly date)
    {
        date = default;
        return TryGetString(element, out var text) && IsoDate.TryParse(text, out date);
    }

    public static bool TryGetPeriodKey(JsonElement? element, out PeriodKey key)
    {
        key = default;
        return TryGetString(element, out var text) && PeriodKey.TryParse(text, out key);
    }

    public static bool TryGetGuid(JsonElement? element, out Guid id)
    {
        id = Guid.Empty;
        return TryGetString(element, out var text) && Guid.TryParse(text, out id);
    }

    public static bool TryGetSafeInteger(JsonElement? element, out double value)
    {
        value = 0;
        return element is { ValueKind: JsonValueKind.Number } number
            && number.TryGetDouble(out value)
            && value == Math.Floor(value)
            && Math.Abs(value) <= MaxSafeInteger;
    }

    public static bool TryGetOptionalText(JsonElement? element, out string? text)
    {
        text = null;
        if (IsMissing(element))
        {
            return true;
        }

        if (!TryGetString(element, out var value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Any(char.IsControl))
        {
            return false;
        }

        text = trimmed;
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
