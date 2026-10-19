using Mita.Domain.Periods;
using Mita.Domain.Users;

namespace Mita.Api.Features.Overview.GetOverview;

public static class OverviewRanges
{
    public static bool TryParse(string? value, out OverviewRange range)
    {
        (var parsed, range) = value switch
        {
            "week" => (true, OverviewRange.Week),
            "month" => (true, OverviewRange.Month),
            "year" => (true, OverviewRange.Year),
            _ => (false, default(OverviewRange)),
        };
        return parsed;
    }

    public static string ToWire(OverviewRange range) => range switch
    {
        OverviewRange.Week => "week",
        OverviewRange.Month => "month",
        OverviewRange.Year => "year",
        _ => throw new ArgumentOutOfRangeException(nameof(range), range, null),
    };

    public static RangePair Of(OverviewRange range, DateOnly today, MonthStart start)
    {
        if (range == OverviewRange.Week)
        {
            return new RangePair(
                new DateRange(today.AddDays(-6), today),
                new DateRange(today.AddDays(-13), today.AddDays(-7)));
        }

        var key = FinancialPeriod.For(today, start).Key;
        DateRange Span(int first, int last) => new(
            FinancialPeriod.Shift(key, first, start).From,
            FinancialPeriod.Shift(key, last, start).To);

        return range == OverviewRange.Month
            ? new RangePair(Span(0, 0), Span(-1, -1))
            : new RangePair(Span(-11, 0), Span(-23, -12));
    }
}
