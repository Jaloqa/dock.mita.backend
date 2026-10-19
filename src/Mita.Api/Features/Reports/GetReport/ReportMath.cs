using Mita.Api.Common.Reporting;

namespace Mita.Api.Features.Reports.GetReport;

public static class ReportMath
{
    public const string DefaultRange = "6m";

    public static double? SavingsRate(long income, long expense)
    {
        if (income <= 0)
        {
            return null;
        }

        var scaled = (Int128)(income - expense) * 1000;
        var magnitude = ((Int128)2 * Int128.Abs(scaled) + income) / ((Int128)2 * income);
        return Shares.Percent((long)(scaled < 0 ? -magnitude : magnitude));
    }

    public static bool TryParseRange(string? value, out int periods)
    {
        periods = value switch
        {
            "3m" => 3,
            "6m" => 6,
            "12m" => 12,
            _ => 0,
        };
        return periods != 0;
    }
}
