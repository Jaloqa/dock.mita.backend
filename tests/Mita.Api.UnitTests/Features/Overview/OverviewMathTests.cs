using Mita.Api.Features.Overview.GetOverview;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Overview;

public sealed class OverviewMathTests
{
    [Theory]
    [InlineData(5_240, 5_000, 4.8)]
    [InlineData(4_500, 5_000, -10.0)]
    [InlineData(5_000, 5_000, 0.0)]
    [InlineData(4_999, 5_000, 0.0)]
    [InlineData(1, 400, -99.7)]
    public void Should_ComputePercentChange(long current, long previous, double expected)
    {
        var change = OverviewMath.PercentChange(current, previous);

        change.ShouldBe(expected);
        double.IsNegative(change!.Value).ShouldBe(expected < 0);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(0, 0)]
    public void Should_ReturnNull_When_PreviousIsZero(long current, long previous)
    {
        OverviewMath.PercentChange(current, previous).ShouldBeNull();
    }
}
