using System.Globalization;

namespace Mita.Api.Common.Wire;

public static class IsoDate
{
    private const string Format = "yyyy-MM-dd";

    public static bool TryParse(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static string ToWire(DateOnly date) => date.ToString(Format, CultureInfo.InvariantCulture);
}
