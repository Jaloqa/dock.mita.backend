using Mita.Api.Common.Groups;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Groups;

public sealed class GroupTermsTests
{
    private static readonly Guid GroupId = Guid.NewGuid();

    private static readonly Guid Anna = Guid.NewGuid();

    private static readonly Guid Olya = Guid.NewGuid();

    private static readonly Guid Max = Guid.NewGuid();

    private static readonly Guid E1 = Guid.NewGuid();

    private static readonly Guid E2 = Guid.NewGuid();

    private static readonly DateOnly Date = new(2026, 10, 1);

    private static readonly DateTimeOffset At = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_DescribeExpense_When_MemberPaidIt()
    {
        var terms = GroupTerms.For(Expense(Anna, 12_000, [Anna, Olya, Max]), Anna);

        terms.ShouldNotBeNull();
        terms.Amount.ShouldBe(12_000);
        terms.Currency.ShouldBe("EUR");
        terms.Date.ShouldBe("2026-10-01");
        terms.Share.ShouldBe(4_000);
        terms.CategoryId.ShouldBe("cafes");
        terms.Tags.ShouldBe(["dinner"]);
        terms.Description.ShouldBe("Dinner");
    }

    [Fact]
    public void Should_ShareNothing_When_PayerIsNotInTheSplit() =>
        GroupTerms.For(Expense(Anna, 300, [Olya]), Anna).ShouldNotBeNull().Share.ShouldBe(0);

    [Fact]
    public void Should_HaveNoExpenseTerms_When_DeletedOrPaidByAnotherMember()
    {
        var deleted = Expense(Anna, 12_000, [Anna, Olya]);
        deleted.Delete(At);

        GroupTerms.For(Expense(Olya, 12_000, [Anna, Olya]), Anna).ShouldBeNull();
        GroupTerms.For(deleted, Anna).ShouldBeNull();
    }

    [Fact]
    public void Should_DescribeRepayment_FromBothSides()
    {
        var repayment = Repayment(Anna, Olya, 5_000, [new GroupCover(E2, 2_000), new GroupCover(E1, 1_000)]);

        var paid = GroupTerms.For(repayment, Anna).ShouldNotBeNull();
        var received = GroupTerms.For(repayment, Olya).ShouldNotBeNull();

        paid.Direction.ShouldBe("paid");
        paid.MemberId.ShouldBe(Olya);
        paid.Amount.ShouldBe(5_000);
        paid.Currency.ShouldBe("EUR");
        paid.Date.ShouldBe("2026-10-01");
        paid.GroupAmount.ShouldBe(5_000);
        paid.Covers.ShouldBe([new GroupCoverDto(E2, 2_000), new GroupCoverDto(E1, 1_000)]);
        received.Direction.ShouldBe("received");
        received.MemberId.ShouldBe(Anna);
        received.Amount.ShouldBe(5_000);
        received.GroupAmount.ShouldBeNull();
        received.Covers.ShouldBeNull();
        GroupTerms.For(repayment, Max).ShouldBeNull();
    }

    [Fact]
    public void Should_WriteRepaymentTerms_WithoutFieldsThatDoNotApply()
    {
        var repayment = Repayment(Anna, Olya, 5_000, [new GroupCover(E2, 2_000)]);

        GroupTerms.Canonical(GroupTerms.For(repayment, Anna)).ShouldBe(
            $$"""{"direction":"paid","memberId":"{{Olya}}","amount":5000,"currency":"EUR","date":"2026-10-01","groupAmount":5000,"covers":[{"expenseId":"{{E2}}","groupAmount":2000}]}""");
        GroupTerms.Canonical(GroupTerms.For(repayment, Olya)).ShouldBe(
            $$"""{"direction":"received","memberId":"{{Anna}}","amount":5000,"currency":"EUR","date":"2026-10-01"}""");
    }

    [Fact]
    public void Should_ChangePaidTerms_When_OnlyTheManualRateChanges()
    {
        var repayment = GroupRepayment.Create(GroupId, PaidInPln(0.25m, 5_000));
        var paid = GroupTerms.Canonical(GroupTerms.For(repayment, Anna));
        var received = GroupTerms.Canonical(GroupTerms.For(repayment, Olya));

        repayment.Update(PaidInPln(0.2m, 4_000));

        GroupTerms.Canonical(GroupTerms.For(repayment, Anna)).ShouldNotBe(paid);
        GroupTerms.For(repayment, Anna).ShouldNotBeNull().GroupAmount.ShouldBe(4_000);
        GroupTerms.Canonical(GroupTerms.For(repayment, Olya)).ShouldBe(received);

        static GroupRepaymentDetails PaidInPln(decimal rate, long groupAmount) =>
            new(Anna, Olya, 20_000, Currency.PLN, Date, rate, true, groupAmount, null, [new GroupCover(E1, 3_000)]);
    }

    [Fact]
    public void Should_HaveNoRepaymentTerms_When_Deleted()
    {
        var repayment = Repayment(Anna, Olya, 5_000, []);
        repayment.Delete(At);

        GroupTerms.For(repayment, Anna).ShouldBeNull();
        GroupTerms.For(repayment, Olya).ShouldBeNull();
    }

    [Fact]
    public void Should_ReadStoredTerms_ToTheSameCanonicalForm()
    {
        var expense = GroupTerms.Canonical(GroupTerms.For(Expense(Anna, 12_000, [Anna, Olya, Max], "Кафе & <bar>"), Anna));
        var repayment = Repayment(Anna, Olya, 5_000, [new GroupCover(E2, 2_000)]);
        var paid = GroupTerms.Canonical(GroupTerms.For(repayment, Anna));
        var received = GroupTerms.Canonical(GroupTerms.For(repayment, Olya));
        const string storedExpense =
            """{"date": "2026-10-01", "tags": ["dinner"], "share": 4000, "amount": 12000, "currency": "EUR", "categoryId": "cafes", "description": "Кафе & <bar>"}""";
        var storedPaid =
            $$"""{"date": "2026-10-01", "covers": [{"groupAmount": 2000, "expenseId": "{{E2}}"}], "amount": 5000, "currency": "EUR", "memberId": "{{Olya}}", "direction": "paid", "groupAmount": 5000}""";
        var storedReceived =
            $$"""{"date": "2026-10-01", "amount": 5000, "currency": "EUR", "memberId": "{{Anna}}", "direction": "received"}""";

        GroupTerms.Canonical(GroupTerms.Read(GroupEntryKind.Expense, storedExpense)).ShouldBe(expense);
        GroupTerms.Canonical(GroupTerms.Read(GroupEntryKind.Repayment, storedPaid)).ShouldBe(paid);
        GroupTerms.Canonical(GroupTerms.Read(GroupEntryKind.Repayment, storedReceived)).ShouldBe(received);
        GroupTerms.Read(GroupEntryKind.Expense, null).ShouldBeNull();
        GroupTerms.Canonical(null).ShouldBeNull();
    }

    private static GroupExpense Expense(Guid payer, long amount, Guid[] between, string description = "Dinner") =>
        GroupExpense.Create(
            GroupId,
            new ExpenseDetails(
                payer,
                amount,
                Currency.EUR,
                Date,
                "cafes",
                null,
                ["dinner"],
                description,
                1m,
                false,
                amount,
                GroupSplitMode.Equal,
                GroupExpenseSplits.Equal(amount, amount, between, payer)));

    private static GroupRepayment Repayment(Guid from, Guid to, long amount, GroupCover[] covers) =>
        GroupRepayment.Create(
            GroupId,
            new GroupRepaymentDetails(from, to, amount, Currency.EUR, Date, 1m, false, amount, null, covers));
}
