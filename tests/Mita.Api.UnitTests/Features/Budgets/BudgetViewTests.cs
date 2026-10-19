using Mita.Api.Common.Budgets;
using Mita.Domain.Budgets;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Budgets;

public sealed class BudgetViewTests
{
    [Theory]
    [InlineData(0, 10_000, BudgetStatus.OnTrack)]
    [InlineData(8_499, 10_000, BudgetStatus.OnTrack)]
    [InlineData(8_500, 10_000, BudgetStatus.AlmostSpent)]
    [InlineData(10_000, 10_000, BudgetStatus.AlmostSpent)]
    [InlineData(10_001, 10_000, BudgetStatus.OverBudget)]
    public void Should_ClassifyStatus_When_SpentIsComparedWithLimit(long spent, long limit, BudgetStatus expected) =>
        BudgetView.Status(spent, limit).ShouldBe(expected);

    [Theory]
    [InlineData(206_430, 18, 11_468L)]
    [InlineData(100, 3, 33L)]
    public void Should_RoundPerDayDown_When_RemainingAndDaysArePositive(long remaining, int daysLeft, long expected) =>
        BudgetView.PerDay(remaining, daysLeft).ShouldBe(expected);

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-500, 10)]
    [InlineData(1_000, 0)]
    public void Should_ReturnNoPerDay_When_NothingRemainsOrNoDaysLeft(long remaining, int daysLeft) =>
        BudgetView.PerDay(remaining, daysLeft).ShouldBeNull();

    [Fact]
    public void Should_SumEveryBudget_When_Summarizing()
    {
        var budgets = new[]
        {
            new BudgetDto(Guid.NewGuid(), Guid.NewGuid(), "2026-10", 10_000, 9_000, 1_000, "almost_spent", 33),
            new BudgetDto(Guid.NewGuid(), Guid.NewGuid(), "2026-10", 10_000, 12_500, -2_500, "over_budget", null)
        };

        BudgetView.Summarize(budgets, 7).ShouldBe(new BudgetSummary(20_000, 21_500, -1_500, 7));
    }

    [Fact]
    public void Should_ReturnEmptySummary_When_NoBudgets() =>
        BudgetView.Summarize([], 0).ShouldBe(new BudgetSummary(0, 0, 0, 0));
}
