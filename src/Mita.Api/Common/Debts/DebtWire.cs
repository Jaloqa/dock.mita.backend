using System.Text.Json;
using Mita.Domain.Debts;

namespace Mita.Api.Common.Debts;

public static class DebtWire
{
    public static string ToWire(DebtDirection direction) => direction switch
    {
        DebtDirection.Lent => "lent",
        DebtDirection.Borrowed => "borrowed",
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
    };

    public static string ToWire(DebtStatus status) => status switch
    {
        DebtStatus.Open => "open",
        DebtStatus.Settled => "settled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static bool TryParseDirection(JsonElement? element, out DebtDirection direction)
    {
        var text = element is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
        (direction, var ok) = text switch
        {
            "lent" => (DebtDirection.Lent, true),
            "borrowed" => (DebtDirection.Borrowed, true),
            _ => (default(DebtDirection), false)
        };
        return ok;
    }

    public static bool TryParseStatus(string? value, out DebtStatus status)
    {
        (status, var ok) = value switch
        {
            "open" => (DebtStatus.Open, true),
            "settled" => (DebtStatus.Settled, true),
            _ => (default(DebtStatus), false)
        };
        return ok;
    }
}
