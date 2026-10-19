using Mita.Api.Common.Reporting;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Reporting;

public sealed class SharesTests
{
    public static TheoryData<long[], int[]> Splits => new()
    {
        { [210_000, 124_000, 42_000, 41_200, 31_000, 45_370], [425, 251, 85, 84, 63, 92] },
        { [1, 1, 1], [334, 333, 333] },
        { [5], [1000] },
        { [0, 0], [0, 0] },
        { [], [] },
    };

    [Theory]
    [MemberData(nameof(Splits))]
    public void Should_SplitIntoTenths(long[] amounts, int[] expected)
    {
        Shares.Tenths(amounts).ShouldBe(expected);
    }

    [Fact]
    public void Should_AddUpToOneThousand_When_AnyAmountIsPositive()
    {
        Shares.Tenths([7, 11, 13, 17, 19, 23, 29]).Sum().ShouldBe(1000);
    }

    [Theory]
    [InlineData(425, 42.5)]
    [InlineData(1000, 100)]
    [InlineData(0, 0)]
    public void Should_ConvertTenthsToPercent(int tenths, double expected)
    {
        Shares.Percent(tenths).ShouldBe(expected);
    }

    [Theory]
    [InlineData(130_001, 6, 21_667)]
    [InlineData(1_000, 3, 333)]
    [InlineData(7_000, 12, 583)]
    [InlineData(80_000, 2, 40_000)]
    [InlineData(5, 2, 3)]
    [InlineData(-5, 2, -3)]
    [InlineData(0, 6, 0)]
    public void Should_RoundAverageHalfAwayFromZero(long total, int count, long expected)
    {
        Shares.RoundedAverage(total, count).ShouldBe(expected);
    }
}
