using Mita.Domain.Groups;
using Shouldly;

namespace Mita.Api.UnitTests.Groups;

public sealed class GroupCoverAllocationTests
{
    private static readonly Guid E1 = Guid.Parse("0199f1c2-0000-7000-8000-0000000000e1");
    private static readonly Guid E2 = Guid.Parse("0199f1c2-0000-7000-8000-0000000000e2");
    private static readonly Guid E3 = Guid.Parse("0199f1c2-0000-7000-8000-0000000000e3");

    private static readonly (Guid ExpenseId, long Open)[] Shares = [(E1, 3_000), (E2, 2_000)];

    [Fact]
    public void Should_CoverEveryShare_When_AmountMatchesTheOpenTotal() =>
        GroupCoverAllocation.Allocate(5_000, Shares, []).ShouldBe([new GroupCover(E1, 3_000), new GroupCover(E2, 2_000)]);

    [Fact]
    public void Should_CoverOldestSharesFirst_When_AmountIsSmaller() =>
        GroupCoverAllocation.Allocate(4_000, Shares, []).ShouldBe([new GroupCover(E1, 3_000), new GroupCover(E2, 1_000)]);

    [Fact]
    public void Should_CoverRequestedSharesFirst() =>
        GroupCoverAllocation.Allocate(4_000, Shares, [E2]).ShouldBe([new GroupCover(E2, 2_000), new GroupCover(E1, 2_000)]);

    [Fact]
    public void Should_LeaveTheRestUncovered_When_AmountExceedsTheOpenTotal() =>
        GroupCoverAllocation.Allocate(6_000, Shares, []).ShouldBe([new GroupCover(E1, 3_000), new GroupCover(E2, 2_000)]);

    [Fact]
    public void Should_SkipShares_When_NothingIsOpen() =>
        GroupCoverAllocation.Allocate(1_000, [(E1, 0), (E2, 2_000)], []).ShouldBe([new GroupCover(E2, 1_000)]);

    [Fact]
    public void Should_CoverPreviousSharesFirstInTheirOrder_When_Reallocated() =>
        GroupCoverAllocation.Reallocate(4_000, Shares, [E2, E1])
            .ShouldBe([new GroupCover(E2, 2_000), new GroupCover(E1, 2_000)]);

    [Fact]
    public void Should_CoverOldestSharesFirst_When_PreviousSharesAreNoLongerOpen() =>
        GroupCoverAllocation.Reallocate(3_000, [(E2, 2_000), (E3, 4_000)], [E1])
            .ShouldBe([new GroupCover(E2, 2_000), new GroupCover(E3, 1_000)]);

    [Fact]
    public void Should_CoverTheRestOldestFirst_When_PreviousSharesAreSmaller() =>
        GroupCoverAllocation.Reallocate(4_000, [(E1, 3_000), (E2, 2_000), (E3, 1_000)], [E3])
            .ShouldBe([new GroupCover(E3, 1_000), new GroupCover(E1, 3_000)]);

    [Fact]
    public void Should_CoverNothing_When_NoShareIsOpen() =>
        GroupCoverAllocation.Reallocate(3_000, [], [E1]).ShouldBeEmpty();
}
