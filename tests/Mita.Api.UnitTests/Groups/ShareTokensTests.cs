using Mita.Domain.Groups;
using Shouldly;

namespace Mita.Api.UnitTests.Groups;

public sealed class ShareTokensTests
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    [Fact]
    public void Should_CreateTokenOfTheAlphabet_When_New()
    {
        var token = ShareTokens.New();

        token.Length.ShouldBe(ShareTokens.Length);
        ShareTokens.Length.ShouldBe(43);
        token.ShouldAllBe(character => Alphabet.Contains(character));
        ShareTokens.IsWellFormed(token).ShouldBeTrue();
    }

    [Fact]
    public void Should_CreateDifferentTokens_When_CalledTwice() =>
        ShareTokens.New().ShouldNotBe(ShareTokens.New());

    [Fact]
    public void Should_AcceptEveryCharacterOfTheAlphabet_When_Checked()
    {
        ShareTokens.IsWellFormed(new string('A', 43)).ShouldBeTrue();
        ShareTokens.IsWellFormed(new string('z', 41) + "-_").ShouldBeTrue();
        ShareTokens.IsWellFormed(new string('7', 43)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Should_RejectMissingToken_When_Checked(string? value) =>
        ShareTokens.IsWellFormed(value).ShouldBeFalse();

    [Theory]
    [InlineData(42)]
    [InlineData(44)]
    public void Should_RejectWrongLength_When_Checked(int length) =>
        ShareTokens.IsWellFormed(new string('a', length)).ShouldBeFalse();

    [Theory]
    [InlineData('+')]
    [InlineData('/')]
    [InlineData('=')]
    [InlineData(' ')]
    [InlineData('.')]
    [InlineData('é')]
    public void Should_RejectForeignCharacter_When_Checked(char character) =>
        ShareTokens.IsWellFormed(new string('a', 42) + character).ShouldBeFalse();
}
