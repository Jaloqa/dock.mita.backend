using Mita.Api.Common.Reporting;

namespace Mita.Api.Features.Overview.GetOverview;

public static class OverviewMath
{
    public static double? PercentChange(long current, long previous)
    {
        if (previous == 0)
        {
            return null;
        }

        var numerator = (Int128)2000 * (current - previous) + previous;
        var denominator = (Int128)2 * previous;
        return Shares.Percent((long)FloorDivide(numerator, denominator));
    }

    private static Int128 FloorDivide(Int128 numerator, Int128 denominator)
    {
        var quotient = numerator / denominator;
        return numerator % denominator != 0 && (numerator < 0) != (denominator < 0) ? quotient - 1 : quotient;
    }
}
