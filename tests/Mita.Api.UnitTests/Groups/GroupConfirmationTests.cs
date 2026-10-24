using Mita.Domain.Groups;
using Shouldly;

namespace Mita.Api.UnitTests.Groups;

public sealed class GroupConfirmationTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_KeepRecordsTerms_When_Decided()
    {
        var confirmation = GroupConfirmation.Create(Guid.NewGuid(), Guid.NewGuid(), GroupEntryKind.Expense, Guid.NewGuid());
        var accountId = Guid.NewGuid();
        confirmation.Confirm("first", accountId, At);

        confirmation.Decide("second", At.AddHours(1));

        confirmation.HandledTerms.ShouldBe("second");
        confirmation.ConfirmedTerms.ShouldBe("first");
        confirmation.AccountId.ShouldBe(accountId);
        confirmation.DecidedAt.ShouldBe(At.AddHours(1));
    }

    [Fact]
    public void Should_RecordTermsAndAccount_When_Confirmed()
    {
        var confirmation = GroupConfirmation.Create(Guid.NewGuid(), Guid.NewGuid(), GroupEntryKind.Repayment, Guid.NewGuid());
        var accountId = Guid.NewGuid();

        confirmation.Confirm("terms", accountId, At);

        confirmation.HandledTerms.ShouldBe("terms");
        confirmation.ConfirmedTerms.ShouldBe("terms");
        confirmation.AccountId.ShouldBe(accountId);
        confirmation.DecidedAt.ShouldBe(At);
    }

    [Fact]
    public void Should_Throw_When_LegMovesNothing() =>
        Should.Throw<ArgumentException>(() =>
            GroupAccountLeg.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 1), 0));
}
