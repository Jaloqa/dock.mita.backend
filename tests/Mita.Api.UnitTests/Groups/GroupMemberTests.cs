using Mita.Domain.Groups;
using Shouldly;

namespace Mita.Api.UnitTests.Groups;

public sealed class GroupMemberTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Should_StartUnlinkedAndUninvited_When_Created()
    {
        var member = NewMember();

        member.UserId.ShouldBeNull();
        member.IsLinked.ShouldBeFalse();
        member.InvitedEmail.ShouldBeNull();
        member.InvitedAt.ShouldBeNull();
    }

    [Fact]
    public void Should_LinkAndClearInvitation_When_LinkedToUser()
    {
        var member = NewMember();
        member.Invite("max@example.com", Now);
        var userId = Guid.NewGuid();

        member.LinkTo(userId);

        member.UserId.ShouldBe(userId);
        member.IsLinked.ShouldBeTrue();
        member.InvitedEmail.ShouldBeNull();
        member.InvitedAt.ShouldBeNull();
    }

    [Fact]
    public void Should_KeepLink_When_LinkedToSameUserAgain()
    {
        var member = NewMember();
        var userId = Guid.NewGuid();
        member.LinkTo(userId);

        member.LinkTo(userId);

        member.UserId.ShouldBe(userId);
        member.IsLinked.ShouldBeTrue();
    }

    [Fact]
    public void Should_Throw_When_LinkedToAnotherUser()
    {
        var member = NewMember();
        var userId = Guid.NewGuid();
        member.LinkTo(userId);

        Should.Throw<InvalidOperationException>(() => member.LinkTo(Guid.NewGuid()));

        member.UserId.ShouldBe(userId);
    }

    [Fact]
    public void Should_Throw_When_LinkedMemberInvited()
    {
        var member = NewMember();
        member.LinkTo(Guid.NewGuid());

        Should.Throw<InvalidOperationException>(() => member.Invite("a@b.pl", Now));

        member.InvitedEmail.ShouldBeNull();
        member.InvitedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("A@b.pl")]
    [InlineData(" a@b.pl")]
    [InlineData("a@b.pl ")]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Throw_When_InvitedEmailNotNormalized(string email)
    {
        var member = NewMember();

        Should.Throw<ArgumentException>(() => member.Invite(email, Now));

        member.InvitedEmail.ShouldBeNull();
        member.InvitedAt.ShouldBeNull();
    }

    [Fact]
    public void Should_Throw_When_InvitedEmailTooLong()
    {
        var member = NewMember();
        var longest = $"{new string('a', GroupMember.EmailMaxLength - 11)}@example.pl";

        Should.Throw<ArgumentException>(() => member.Invite($"a{longest}", Now));
        member.Invite(longest, Now);

        longest.Length.ShouldBe(254);
        member.InvitedEmail.ShouldBe(longest);
    }

    [Fact]
    public void Should_ReplaceInvitation_When_InvitedAgain()
    {
        var member = NewMember();
        member.Invite("a@b.pl", Now);

        member.Invite("c@d.pl", Now.AddHours(1));

        member.InvitedEmail.ShouldBe("c@d.pl");
        member.InvitedAt.ShouldBe(Now.AddHours(1));
        member.IsLinked.ShouldBeFalse();
    }

    [Fact]
    public void Should_ClearUser_When_Unlinked()
    {
        var member = NewMember();
        member.LinkTo(Guid.NewGuid());

        member.Unlink();

        member.UserId.ShouldBeNull();
        member.IsLinked.ShouldBeFalse();
    }

    [Fact]
    public void Should_ClearEmailAndTime_When_InvitationCancelled()
    {
        var member = NewMember();
        member.Invite("a@b.pl", Now);

        member.CancelInvite();

        member.InvitedEmail.ShouldBeNull();
        member.InvitedAt.ShouldBeNull();
    }

    private static GroupMember NewMember() => GroupMember.Create(Guid.NewGuid(), "Olya", 1);
}
