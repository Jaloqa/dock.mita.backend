using Mita.Domain.Groups;
using Shouldly;

namespace Mita.Api.UnitTests.Groups;

public sealed class GroupEntryEventTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_RecordUser_When_OwnerActs()
    {
        var userId = Guid.NewGuid();

        var entryEvent = Create(GroupActor.Owner(userId));

        entryEvent.ActorUserId.ShouldBe(userId);
        entryEvent.ActorMemberId.ShouldBeNull();
    }

    [Fact]
    public void Should_RecordMember_When_MemberActs()
    {
        var memberId = Guid.NewGuid();

        var entryEvent = Create(GroupActor.Member(memberId));

        entryEvent.ActorUserId.ShouldBeNull();
        entryEvent.ActorMemberId.ShouldBe(memberId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Should_Throw_When_ActorIsNotExactlyOneUserOrMember(bool withUser, bool withMember)
    {
        var actor = new GroupActor(withUser ? Guid.NewGuid() : null, withMember ? Guid.NewGuid() : null);

        Should.Throw<ArgumentException>(() => Create(actor)).ParamName.ShouldBe("actor");
    }

    private static GroupEntryEvent Create(GroupActor actor) => GroupEntryEvent.Create(
        Guid.NewGuid(),
        GroupEntryKind.Expense,
        Guid.NewGuid(),
        GroupEntryAction.Created,
        At,
        actor,
        "{}");
}
