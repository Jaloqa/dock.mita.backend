using System.Text.Json;

namespace Mita.Api.Common.Rates;

public static class ManualRate
{
    public const decimal Max = 1_000_000_000m;

    public static bool TryParse(JsonElement? element, out decimal rate)
    {
        rate = 0;
        return element is { ValueKind: JsonValueKind.Number } number
            && number.TryGetDecimal(out rate)
            && rate > 0
            && rate <= Max;
    }
}
