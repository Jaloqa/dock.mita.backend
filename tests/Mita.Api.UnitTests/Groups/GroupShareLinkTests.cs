using Mita.Domain.Groups;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Groups;

public sealed class GroupShareLinkTests
{
    [Fact]
    public void Should_HaveLinkOff_When_GroupCreated() =>
        NewGroup().ShareToken.ShouldBeNull();

    [Fact]
    public void Should_StoreToken_When_LinkTurnedOn()
    {
        var group = NewGroup();
        var token = ShareTokens.New();

        group.TurnOnShareLink(token);

        group.ShareToken.ShouldBe(token);
    }

    [Fact]
    public void Should_ReplaceToken_When_LinkTurnedOnAgain()
    {
        var group = NewGroup();
        group.TurnOnShareLink(ShareTokens.New());
        var replacement = ShareTokens.New();

        group.TurnOnShareLink(replacement);

        group.ShareToken.ShouldBe(replacement);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("++++++++++++++++++++++++++++++++++++++++++=")]
    public void Should_Throw_When_TokenMalformed(string token)
    {
        var group = NewGroup();
        var previous = ShareTokens.New();
        group.TurnOnShareLink(previous);

        Should.Throw<ArgumentException>(() => group.TurnOnShareLink(token));

        group.ShareToken.ShouldBe(previous);
    }

    [Fact]
    public void Should_ClearToken_When_LinkTurnedOff()
    {
        var group = NewGroup();
        group.TurnOnShareLink(ShareTokens.New());

        group.TurnOffShareLink();
        group.TurnOffShareLink();

        group.ShareToken.ShouldBeNull();
    }

    private static Group NewGroup() => Group.Create(Guid.NewGuid(), "Trip", Currency.EUR);
}
