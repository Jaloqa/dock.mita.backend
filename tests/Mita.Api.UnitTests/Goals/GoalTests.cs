using Mita.Domain.Goals;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Goals;

public sealed class GoalTests
{
    [Fact]
    public void Should_KeepTargetAtOneMinorUnit_When_RebaseRoundsToZero()
    {
        var goal = Goal.Create(Guid.NewGuid(), "Bike", 1, null, null, Currency.UAH);
        goal.AddSaved(1);

        goal.Rebase(Currency.PLN, 0.1m);

        goal.Target.ShouldBe(1);
        goal.Saved.ShouldBe(0);
        goal.Currency.ShouldBe(Currency.PLN);
    }

    [Fact]
    public void Should_ConvertTargetAndSaved_When_Rebased()
    {
        var goal = Goal.Create(Guid.NewGuid(), "Bike", 10_000, null, null, Currency.EUR);
        goal.AddSaved(2_500);

        goal.Rebase(Currency.PLN, 4.25m);

        goal.Target.ShouldBe(42_500);
        goal.Saved.ShouldBe(10_625);
    }
}
