using Mita.Domain.Periods;
using Mita.Domain.Users;
using Shouldly;

namespace Mita.Api.UnitTests.Periods;

public sealed class FinancialPeriodTests
{
    [Fact]
    public void Should_ReturnCalendarMonth_When_StartIsFirstDay()
    {
        var period = FinancialPeriod.For(new DateOnly(2026, 10, 14), MonthStart.Day(1));

        period.Key.ToString().ShouldBe("2026-10");
        period.From.ShouldBe(new DateOnly(2026, 10, 1));
        period.To.ShouldBe(new DateOnly(2026, 10, 31));
    }

    [Fact]
    public void Should_ReturnPreviousMonthKey_When_DateIsBeforeStartDay()
    {
        var period = FinancialPeriod.For(new DateOnly(2026, 10, 5), MonthStart.Day(10));

        period.Key.ToString().ShouldBe("2026-09");
        period.From.ShouldBe(new DateOnly(2026, 9, 10));
        period.To.ShouldBe(new DateOnly(2026, 10, 9));
    }

    [Fact]
    public void Should_ReturnCurrentMonthKey_When_DateIsOnStartDay()
    {
        var period = FinancialPeriod.For(new DateOnly(2026, 10, 10), MonthStart.Day(10));

        period.Key.ToString().ShouldBe("2026-10");
        period.From.ShouldBe(new DateOnly(2026, 10, 10));
        period.To.ShouldBe(new DateOnly(2026, 11, 9));
    }

    [Fact]
    public void Should_CrossYearBoundary_When_DateIsInJanuaryBeforeStartDay()
    {
        var period = FinancialPeriod.For(new DateOnly(2027, 1, 5), MonthStart.Day(10));

        period.Key.ToString().ShouldBe("2026-12");
        period.From.ShouldBe(new DateOnly(2026, 12, 10));
        period.To.ShouldBe(new DateOnly(2027, 1, 9));
    }

    [Fact]
    public void Should_ShiftForwardAcrossYear()
    {
        var period = FinancialPeriod.Shift(new PeriodKey(2026, 12), 1, MonthStart.Day(1));

        period.Key.ToString().ShouldBe("2027-01");
        period.From.ShouldBe(new DateOnly(2027, 1, 1));
        period.To.ShouldBe(new DateOnly(2027, 1, 31));
    }

    [Fact]
    public void Should_ShiftBackAcrossYear()
    {
        FinancialPeriod.Shift(new PeriodKey(2027, 1), -1, MonthStart.Day(1)).Key.ToString().ShouldBe("2026-12");
    }

    [Fact]
    public void Should_StartOnLastWorkingDayOfPreviousMonth_When_StartIsLastWorkingDay()
    {
        var period = FinancialPeriod.ByKey(new PeriodKey(2026, 10), MonthStart.LastWorkingDay);

        period.From.ShouldBe(new DateOnly(2026, 9, 30));
        period.To.ShouldBe(new DateOnly(2026, 10, 29));
    }

    [Fact]
    public void Should_RollToNextKey_When_DateIsLastWorkingDayOfMonth()
    {
        FinancialPeriod.For(new DateOnly(2026, 10, 30), MonthStart.LastWorkingDay).Key.ToString().ShouldBe("2026-11");
        FinancialPeriod.For(new DateOnly(2026, 10, 29), MonthStart.LastWorkingDay).Key.ToString().ShouldBe("2026-10");
    }

    [Fact]
    public void Should_ContainEveryDayOfYear_ForEveryStart()
    {
        MonthStart[] starts = [MonthStart.Day(1), MonthStart.Day(10), MonthStart.Day(28), MonthStart.LastWorkingDay];

        foreach (var start in starts)
        {
            for (var day = new DateOnly(2026, 1, 1); day.Year == 2026; day = day.AddDays(1))
            {
                var period = FinancialPeriod.For(day, start);

                (day >= period.From && day <= period.To).ShouldBeTrue($"{day} for {start}");
            }
        }
    }

    [Theory]
    [InlineData(2026, 10, 14, 18)]
    [InlineData(2026, 10, 31, 1)]
    [InlineData(2026, 11, 2, 0)]
    [InlineData(2026, 9, 20, 31)]
    public void Should_CountDaysLeft(int year, int month, int day, int expected)
    {
        var period = FinancialPeriod.ByKey(new PeriodKey(2026, 10), MonthStart.Day(1));

        period.DaysLeft(new DateOnly(year, month, day)).ShouldBe(expected);
    }

    [Theory]
    [InlineData("2026-10", true)]
    [InlineData("1970-01", true)]
    [InlineData("2099-12", true)]
    [InlineData("2026-13", false)]
    [InlineData("2026-00", false)]
    [InlineData("2026-1", false)]
    [InlineData("0026-10", false)]
    [InlineData("0000-01", false)]
    [InlineData("2100-01", false)]
    [InlineData("2026-10-01", false)]
    [InlineData("2026-10\n", false)]
    [InlineData("20٢٦-10", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Should_ParseOnlyValidKeys(string? text, bool expected)
    {
        PeriodKey.TryParse(text, out _).ShouldBe(expected);
    }

    [Fact]
    public void Should_ParseKeyComponents()
    {
        PeriodKey.TryParse("2026-10", out var key).ShouldBeTrue();

        key.ShouldBe(new PeriodKey(2026, 10));
    }

    [Fact]
    public void Should_AddMonths_AcrossYears()
    {
        new PeriodKey(2026, 10).AddMonths(3).ToString().ShouldBe("2027-01");
        new PeriodKey(2026, 10).AddMonths(-10).ToString().ShouldBe("2025-12");
    }

    [Fact]
    public void Should_OrderKeysChronologically()
    {
        new PeriodKey(2026, 12).CompareTo(new PeriodKey(2027, 1)).ShouldBeLessThan(0);
        new PeriodKey(2026, 10).MonthIndex.ShouldBe(2026 * 12 + 10);
    }
}
