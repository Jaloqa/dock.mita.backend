using System.Text.Json;
using Mita.Domain.Money;

namespace Mita.Api.Common.Money;

public static class CurrencyWire
{
    public static string ToWire(Currency currency) => currency switch
    {
        Currency.PLN => "PLN",
        Currency.EUR => "EUR",
        Currency.USD => "USD",
        Currency.UAH => "UAH",
        _ => throw new ArgumentOutOfRangeException(nameof(currency), currency, null)
    };

    public static bool TryParse(string? value, out Currency currency)
    {
        (currency, var ok) = value switch
        {
            "PLN" => (Currency.PLN, true),
            "EUR" => (Currency.EUR, true),
            "USD" => (Currency.USD, true),
            "UAH" => (Currency.UAH, true),
            _ => (default(Currency), false)
        };
        return ok;
    }

    public static bool TryParse(JsonElement? element, out Currency currency) =>
        TryParse(element is { ValueKind: JsonValueKind.String } text ? text.GetString() : null, out currency);
}
