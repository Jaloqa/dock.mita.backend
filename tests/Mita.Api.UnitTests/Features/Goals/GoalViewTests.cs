using Mita.Api.Common.Goals;
using Mita.Domain.Goals;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Goals;

public sealed class GoalViewTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);

    [Theory]
    [InlineData("2027-06", "2026-10-16", 8)]
    [InlineData("2026-10", "2026-10-31", 0)]
    [InlineData("2027-01", "2026-12-31", 1)]
    [InlineData("2026-09", "2026-10-01", -1)]
    public void Should_CountCalendarMonths_When_DeadlineIsCompared(string deadline, string today, int expected) =>
        GoalView.MonthsUntil(Key(deadline), DateOnly.Parse(today, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(expected);

    [Theory]
    [InlineData(380_000, "2027-06", 47_500L)]
    [InlineData(110_000, "2027-01", 36_667L)]
    [InlineData(50_000, "2026-10", 50_000L)]
    public void Should_SpreadRemainingOverMonths_When_DeadlineIsAhead(long remaining, string deadline, long expected) =>
        GoalView.Monthly(remaining, Key(deadline), Today).ShouldBe(expected);

    [Theory]
    [InlineData(50_000, null)]
    [InlineData(0, "2027-06")]
    [InlineData(50_000, "2026-09")]
    public void Should_ReturnNoMonthly_When_NoDeadlineNothingLeftOrOverdue(long remaining, string? deadline) =>
        GoalView.Monthly(remaining, deadline is null ? null : Key(deadline), Today).ShouldBeNull();

    [Theory]
    [InlineData(800_000, 800_000, null, GoalStatus.Reached)]
    [InlineData(900_000, 800_000, "2026-01", GoalStatus.Reached)]
    [InlineData(100, 800_000, "2026-09", GoalStatus.Overdue)]
    [InlineData(100, 800_000, "2026-10", GoalStatus.Active)]
    [InlineData(100, 800_000, null, GoalStatus.Active)]
    public void Should_ClassifyStatus_When_SavedIsComparedWithTarget(long saved, long target, string? deadline, GoalStatus expected) =>
        GoalView.Status(saved, target, deadline is null ? null : Key(deadline), Today).ShouldBe(expected);

    [Theory]
    [InlineData("2026-10-16", "2026-07-16")]
    [InlineData("2026-05-31", "2026-02-28")]
    [InlineData("2026-02-15", "2025-11-15")]
    public void Should_StartWindowThreeMonthsBefore_When_ClampedToMonthEnd(string today, string expected) =>
        GoalView.SavingWindowStart(DateOnly.Parse(today, System.Globalization.CultureInfo.InvariantCulture))
            .ShouldBe(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public void Should_ReportReached_When_SavedExceedsTarget()
    {
        var goal = Goal.Create(Guid.NewGuid(), "Laptop", 10_000, Key("2027-06"), null, Currency.PLN);
        goal.AddSaved(15_000);

        var view = GoalView.From(goal, 1m, Currency.PLN, Today);

        view.Target.ShouldBe(10_000);
        view.Saved.ShouldBe(15_000);
        view.Remaining.ShouldBe(0);
        view.Percent.ShouldBe(150);
        view.Status.ShouldBe("reached");
        view.Monthly.ShouldBeNull();
        view.Deadline.ShouldBe("2027-06");
        view.Currency.ShouldBe("PLN");
    }

    [Fact]
    public void Should_ConvertTargetAndSaved_When_RateGiven()
    {
        var goal = Goal.Create(Guid.NewGuid(), "Trip", 800_000, null, null, Currency.PLN);
        goal.AddSaved(300_000);

        var view = GoalView.From(goal, 0.235294m, Currency.EUR, Today);

        view.Target.ShouldBe(188_235);
        view.Saved.ShouldBe(70_588);
        view.Remaining.ShouldBe(117_647);
        view.Percent.ShouldBe(37);
        view.Currency.ShouldBe("EUR");
    }

    private static PeriodKey Key(string text) =>
        PeriodKey.TryParse(text, out var key) ? key : throw new ArgumentException("Invalid period.", nameof(text));
}
