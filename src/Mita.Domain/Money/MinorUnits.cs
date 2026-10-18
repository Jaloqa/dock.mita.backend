namespace Mita.Domain.Money;

public static class MinorUnits
{
    public const long MaxAmount = 100_000_000_000;

    public static long Convert(long minor, decimal rate) =>
        (long)Math.Round(minor * rate, 0, MidpointRounding.AwayFromZero);
}
