using Mita.Domain.Groups;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Groups;

public sealed class GroupExpenseTests
{
    private static readonly Guid A = Guid.NewGuid();

    private static readonly Guid B = Guid.NewGuid();

    private static readonly Guid C = Guid.NewGuid();

    private static readonly Guid D = Guid.NewGuid();

    private static readonly DateOnly Date = new(2026, 10, 1);

    private static readonly DateTimeOffset At = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_GiveTheRemainderToThePayer_When_SplitEqually()
    {
        var shares = GroupExpenseSplits.Equal(10_000, 10_000, [A, B, C], B);

        shares.Select(share => share.MemberId).ShouldBe([A, B, C]);
        shares.Select(share => share.Amount).ShouldBe([3_333, 3_334, 3_333]);
        shares.Select(share => share.GroupAmount).ShouldBe([3_333, 3_334, 3_333]);
    }

    [Fact]
    public void Should_SplitTheGroupAmountByShares_When_CurrenciesDiffer()
    {
        var shares = GroupExpenseSplits.Equal(300_000, 3_070, [A, B, C, D], B);

        shares.Select(share => share.Amount).ShouldBe([75_000, 75_000, 75_000, 75_000]);
        shares.Select(share => share.GroupAmount).ShouldBe([768, 768, 767, 767]);
    }

    [Fact]
    public void Should_GiveTheRemainderInMemberOrder_When_PayerDoesNotShare()
    {
        var shares = GroupExpenseSplits.Equal(10_000, 10_000, [B, C, D], A);

        shares.Select(share => share.MemberId).ShouldBe([B, C, D]);
        shares.Select(share => share.Amount).ShouldBe([3_334, 3_333, 3_333]);
        shares.Select(share => share.GroupAmount).ShouldBe([3_334, 3_333, 3_333]);
    }

    [Fact]
    public void Should_DropMembersWithoutAMinorUnit_When_EqualSplitIsBelowTheMemberCount()
    {
        var shares = GroupExpenseSplits.Equal(2, 2, [A, B, C], B);

        shares.Select(share => share.MemberId).ShouldBe([A, B]);
        shares.Select(share => share.Amount).ShouldBe([1, 1]);
        shares.Select(share => share.GroupAmount).ShouldBe([1, 1]);
    }

    [Fact]
    public void Should_KeepOnlyThePayer_When_OneMinorUnitIsSplitEqually()
    {
        var shares = GroupExpenseSplits.Equal(1, 1, [A, B], B);

        shares.ShouldBe(new[] { new GroupShare(B, 1, 1) });
    }

    [Fact]
    public void Should_GiveTheUnitsInMemberOrder_When_PayerDoesNotShareAndAmountIsBelowTheMemberCount()
    {
        var shares = GroupExpenseSplits.Equal(2, 2, [A, B, C], D);

        shares.Select(share => share.MemberId).ShouldBe([A, B]);
        shares.Select(share => share.Amount).ShouldBe([1, 1]);
    }

    [Fact]
    public void Should_AllocateTheGroupAmountBetweenTheRemainingMembers_When_SomeAreDropped()
    {
        var shares = GroupExpenseSplits.Equal(2, 5, [A, B, C], B);

        shares.Select(share => share.MemberId).ShouldBe([A, B]);
        shares.Select(share => share.Amount).ShouldBe([1, 1]);
        shares.Select(share => share.GroupAmount).ShouldBe([2, 3]);
    }

    [Fact]
    public void Should_CreateExpense_When_EqualSplitIsBelowTheMemberCount()
    {
        var shares = GroupExpenseSplits.Equal(2, 2, [A, B, C], B);

        var expense = GroupExpense.Create(Guid.NewGuid(), Details(shares, amount: 2));

        expense.Shares.Select(share => (share.MemberId, share.Amount, share.GroupAmount))
            .ShouldBe([(A, 1L, 1L), (B, 1L, 1L)], ignoreOrder: true);
    }

    [Fact]
    public void Should_SplitTheGroupAmountByExactShares()
    {
        var (olya, max) = (Guid.NewGuid(), Guid.NewGuid());

        var shares = GroupExpenseSplits.Exact(7_500, [(olya, 10_000), (max, 20_000)], olya);

        shares.ShouldBe(new[] { new GroupShare(olya, 10_000, 2_500), new GroupShare(max, 20_000, 5_000) });
    }

    [Fact]
    public void Should_CreateExpense_WithItsShares()
    {
        var groupId = Guid.NewGuid();
        var shares = GroupExpenseSplits.Equal(10_000, 10_000, [A, B, C], A);

        var expense = GroupExpense.Create(groupId, Details(shares));

        expense.Id.ShouldNotBe(Guid.Empty);
        expense.GroupId.ShouldBe(groupId);
        expense.PayerId.ShouldBe(A);
        expense.Amount.ShouldBe(10_000);
        expense.StandardCategory.ShouldBe(StandardGroupCategories.Default);
        expense.CategoryId.ShouldBeNull();
        expense.Split.ShouldBe(GroupSplitMode.Equal);
        expense.IsDeleted.ShouldBeFalse();
        expense.Shares.Select(share => (share.MemberId, share.Amount, share.GroupAmount))
            .ShouldBe([(A, 3_334L, 3_334L), (B, 3_333L, 3_333L), (C, 3_333L, 3_333L)], ignoreOrder: true);
        expense.Shares.ShouldAllBe(share => share.ExpenseId == expense.Id && share.GroupId == groupId);
    }

    [Fact]
    public void Should_ReplaceShares_When_Updated()
    {
        var expense = GroupExpense.Create(Guid.NewGuid(), Details(GroupExpenseSplits.Equal(10_000, 10_000, [A, B, C], A)));

        expense.Update(Details(GroupExpenseSplits.Exact(12_000, [(B, 2_000), (D, 10_000)], A), amount: 12_000));

        expense.Amount.ShouldBe(12_000);
        expense.Shares.Select(share => (share.MemberId, share.Amount, share.GroupAmount))
            .ShouldBe([(B, 2_000L, 2_000L), (D, 10_000L, 10_000L)], ignoreOrder: true);
    }

    [Fact]
    public void Should_Throw_When_SharesDoNotAddUpToAmount()
    {
        GroupShare[] shares = [new(A, 5_000, 5_000), new(B, 4_999, 5_000)];

        Should.Throw<ArgumentException>(() => GroupExpense.Create(Guid.NewGuid(), Details(shares)));
    }

    [Fact]
    public void Should_Throw_When_ShareGroupAmountsDoNotAddUpToGroupAmount()
    {
        GroupShare[] shares = [new(A, 5_000, 5_000), new(B, 5_000, 4_999)];

        Should.Throw<ArgumentException>(() => GroupExpense.Create(Guid.NewGuid(), Details(shares)));
    }

    [Fact]
    public void Should_Throw_When_BothCategoryFieldsAreSet()
    {
        var shares = GroupExpenseSplits.Equal(10_000, 10_000, [A, B], A);

        Should.Throw<ArgumentException>(() => GroupExpense.Create(
            Guid.NewGuid(),
            Details(shares, standardCategory: StandardGroupCategories.Default, categoryId: Guid.NewGuid())));
    }

    [Fact]
    public void Should_Throw_When_NoCategoryIsSet()
    {
        var shares = GroupExpenseSplits.Equal(10_000, 10_000, [A, B], A);

        Should.Throw<ArgumentException>(() => GroupExpense.Create(Guid.NewGuid(), Details(shares, standardCategory: null)));
    }

    [Fact]
    public void Should_Throw_When_SharesRepeatAMember()
    {
        GroupShare[] shares = [new(A, 5_000, 5_000), new(A, 5_000, 5_000)];

        Should.Throw<ArgumentException>(() => GroupExpense.Create(Guid.NewGuid(), Details(shares)));
    }

    [Fact]
    public void Should_DeleteAndRestoreExpense()
    {
        var expense = GroupExpense.Create(Guid.NewGuid(), Details(GroupExpenseSplits.Equal(10_000, 10_000, [A, B], A)));

        expense.Delete(At);
        expense.IsDeleted.ShouldBeTrue();
        expense.DeletedAt.ShouldBe(At);

        expense.Restore();
        expense.IsDeleted.ShouldBeFalse();
        expense.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Should_Throw_When_DeletedTwice()
    {
        var expense = GroupExpense.Create(Guid.NewGuid(), Details(GroupExpenseSplits.Equal(10_000, 10_000, [A, B], A)));
        expense.Delete(At);

        Should.Throw<InvalidOperationException>(() => expense.Delete(At));
    }

    [Fact]
    public void Should_Throw_When_RestoringALiveExpense()
    {
        var expense = GroupExpense.Create(Guid.NewGuid(), Details(GroupExpenseSplits.Equal(10_000, 10_000, [A, B], A)));

        Should.Throw<InvalidOperationException>(expense.Restore);
    }

    [Fact]
    public void Should_Throw_When_RepaymentMembersAreEqual() =>
        Should.Throw<ArgumentException>(() => GroupRepayment.Create(
            Guid.NewGuid(),
            new GroupRepaymentDetails(A, A, 1_000, Currency.EUR, Date, 1m, false, 1_000, null)));

    private static ExpenseDetails Details(
        IReadOnlyList<GroupShare> shares,
        long amount = 10_000,
        string? standardCategory = StandardGroupCategories.Default,
        Guid? categoryId = null) =>
        new(
            A,
            amount,
            Currency.EUR,
            Date,
            standardCategory,
            categoryId,
            [],
            null,
            1m,
            false,
            amount,
            GroupSplitMode.Equal,
            shares);
}
