using System.Text.Json;
using Mita.Domain.Accounts;

namespace Mita.Api.Common.Accounts;

public static class AccountWire
{
    public static string ToWire(AccountType type) => type switch
    {
        AccountType.Card => "card",
        AccountType.Cash => "cash",
        AccountType.Savings => "savings",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static bool TryParse(JsonElement? element, out AccountType type)
    {
        var text = element is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
        (type, var ok) = text switch
        {
            "card" => (AccountType.Card, true),
            "cash" => (AccountType.Cash, true),
            "savings" => (AccountType.Savings, true),
            _ => (default(AccountType), false)
        };
        return ok;
    }
}
