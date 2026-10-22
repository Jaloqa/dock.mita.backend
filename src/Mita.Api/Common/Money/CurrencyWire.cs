using System.Text.Json;
using Mita.Domain.Money;

namespace Mita.Api.Common.Money;

public static class CurrencyWire
{
    public static string ToWire(Currency currency) => currency.ToString();

    public static bool TryParse(string? value, out Currency currency) =>
        Currencies.TryParse(value, out currency);

    public static bool TryParse(JsonElement? element, out Currency currency) =>
        TryParse(element is { ValueKind: JsonValueKind.String } text ? text.GetString() : null, out currency);
}
