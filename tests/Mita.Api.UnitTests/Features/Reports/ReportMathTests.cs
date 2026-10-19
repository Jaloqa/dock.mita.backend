using Mita.Api.Features.Reports.GetReport;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Reports;

public sealed class ReportMathTests
{
    [Theory]
    [InlineData(300_000, 200_000, 33.3)]
    [InlineData(1_000, 0, 100.0)]
    [InlineData(100_000, 150_000, -50.0)]
    [InlineData(2_000, 1_999, 0.1)]
    [InlineData(100_000, 100_040, 0.0)]
    [InlineData(2_000, 2_001, -0.1)]
    [InlineData(2_000, 2_000, 0.0)]
    [InlineData(100_000, 100_004, 0.0)]
    public void Should_ComputeSavingsRate(long income, long expense, double expected)
    {
        var rate = ReportMath.SavingsRate(income, expense);

        rate.ShouldBe(expected);
        double.IsNegative(rate!.Value).ShouldBe(expected < 0);
    }

    [Theory]
    [InlineData(0, 5_000)]
    [InlineData(0, 0)]
    public void Should_ReturnNullRate_When_IncomeIsNotPositive(long income, long expense)
    {
        ReportMath.SavingsRate(income, expense).ShouldBeNull();
    }

    [Theory]
    [InlineData("3m", true, 3)]
    [InlineData("6m", true, 6)]
    [InlineData("12m", true, 12)]
    [InlineData("1m", false, 0)]
    [InlineData("6M", false, 0)]
    [InlineData("year", false, 0)]
    [InlineData("6", false, 0)]
    [InlineData("", false, 0)]
    [InlineData(null, false, 0)]
    public void Should_ParseOnlySupportedRanges(string? value, bool expected, int expectedPeriods)
    {
        ReportMath.TryParseRange(value, out var periods).ShouldBe(expected);
        periods.ShouldBe(expectedPeriods);
    }

    [Fact]
    public void Should_DefaultToSixMonths()
    {
        ReportMath.DefaultRange.ShouldBe("6m");
    }
}
