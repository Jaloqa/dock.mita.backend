namespace Mita.Domain.Money;

public static class MinorUnits
{
    public const long MaxAmount = 100_000_000_000;

    public static IReadOnlyList<Currency> ZeroDecimal { get; } =
    [
        Currency.BIF, Currency.CLP, Currency.DJF, Currency.GNF, Currency.ISK, Currency.JPY, Currency.KMF, Currency.KRW,
        Currency.PYG, Currency.RWF, Currency.UGX, Currency.VND, Currency.VUV, Currency.XAF, Currency.XOF, Currency.XPF
    ];

    public static IReadOnlyList<Currency> ThreeDecimal { get; } =
    [
        Currency.BHD, Currency.IQD, Currency.JOD, Currency.KWD, Currency.LYD, Currency.OMR, Currency.TND
    ];

    public static int Exponent(Currency currency) =>
        ZeroDecimal.Contains(currency) ? 0 : ThreeDecimal.Contains(currency) ? 3 : 2;

    public static long Convert(long minor, decimal rate, Currency from, Currency to)
    {
        var shift = Exponent(to) - Exponent(from);
        var scale = 1m;
        for (var step = 0; step < Math.Abs(shift); step++)
        {
            scale *= 10m;
        }

        var converted = shift >= 0 ? minor * rate * scale : minor * rate / scale;
        return (long)Math.Clamp(Math.Round(converted, 0, MidpointRounding.AwayFromZero), long.MinValue, long.MaxValue);
    }
}
