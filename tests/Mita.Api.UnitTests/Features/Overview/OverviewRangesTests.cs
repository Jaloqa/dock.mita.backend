using System.Globalization;
using Mita.Api.Features.Overview.GetOverview;
using Mita.Domain.Users;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Overview;

public sealed class OverviewRangesTests
{
    public static TheoryData<OverviewRange, int, string, string, string, string> Ranges => new()
    {
        { OverviewRange.Week, 1, "2026-10-10", "2026-10-16", "2026-10-03", "2026-10-09" },
        { OverviewRange.Month, 1, "2026-10-01", "2026-10-31", "2026-09-01", "2026-09-30" },
        { OverviewRange.Month, 10, "2026-10-10", "2026-11-09", "2026-09-10", "2026-10-09" },
        { OverviewRange.Year, 1, "2025-11-01", "2026-10-31", "2024-11-01", "2025-10-31" },
        { OverviewRange.Year, 10, "2025-11-10", "2026-11-09", "2024-11-10", "2025-11-09" },
    };

    [Theory]
    [MemberData(nameof(Ranges))]
    public void Should_ResolveRanges(
        OverviewRange range,
        int startDay,
        string from,
        string to,
        string previousFrom,
        string previousTo)
    {
        var pair = OverviewRanges.Of(range, new DateOnly(2026, 10, 16), MonthStart.Day(startDay));
        DateOnly Date(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture);

        pair.Current.ShouldBe(new DateRange(Date(from), Date(to)));
        pair.Previous.ShouldBe(new DateRange(Date(previousFrom), Date(previousTo)));
    }

    [Theory]
    [InlineData("week", true, OverviewRange.Week)]
    [InlineData("month", true, OverviewRange.Month)]
    [InlineData("year", true, OverviewRange.Year)]
    [InlineData("day", false, OverviewRange.Week)]
    [InlineData("2026-10", false, OverviewRange.Week)]
    [InlineData("Week", false, OverviewRange.Week)]
    [InlineData("", false, OverviewRange.Week)]
    [InlineData(null, false, OverviewRange.Week)]
    public void Should_ParseOnlyExactNames(string? value, bool expected, OverviewRange expectedRange)
    {
        OverviewRanges.TryParse(value, out var range).ShouldBe(expected);

        if (expected)
        {
            range.ShouldBe(expectedRange);
        }
    }

    [Theory]
    [InlineData(OverviewRange.Week, "week")]
    [InlineData(OverviewRange.Month, "month")]
    [InlineData(OverviewRange.Year, "year")]
    public void Should_WriteWireNames(OverviewRange range, string expected)
    {
        OverviewRanges.ToWire(range).ShouldBe(expected);
    }
}
