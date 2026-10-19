using Mita.Api.Common.Time;
using Shouldly;

namespace Mita.Api.UnitTests.Common;

public sealed class UtcCalendarTests
{
    private static readonly FixedTimeProvider Time = new(new DateTimeOffset(2026, 10, 19, 23, 59, 0, TimeSpan.Zero));

    [Fact]
    public void Should_ReturnUtcDate_When_AskedForToday() =>
        UtcCalendar.Today(Time).ShouldBe(new DateOnly(2026, 10, 19));

    [Fact]
    public void Should_ReturnTodayPlusOneYear_When_AskedForLatestAllowedDate() =>
        UtcCalendar.LatestAllowedDate(Time).ShouldBe(new DateOnly(2027, 10, 19));

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
