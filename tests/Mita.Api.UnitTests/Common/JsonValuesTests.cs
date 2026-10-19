using System.Text.Json;
using Mita.Api.Common.Wire;
using Shouldly;

namespace Mita.Api.UnitTests.Common;

public sealed class JsonValuesTests
{
    private static JsonElement? Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Theory]
    [InlineData("\"2026-02-30\"")]
    [InlineData("\"2026-1-01\"")]
    [InlineData("5")]
    public void Should_RejectDate_When_NotIsoDate(string json) =>
        JsonValues.TryGetIsoDate(Parse(json), out _).ShouldBeFalse();

    [Fact]
    public void Should_ParseDate_When_IsoDate()
    {
        JsonValues.TryGetIsoDate(Parse("\"2026-03-02\""), out var date).ShouldBeTrue();
        date.ShouldBe(new DateOnly(2026, 3, 2));
    }

    [Fact]
    public void Should_RejectGuid_When_NotGuid() =>
        JsonValues.TryGetGuid(Parse("\"acc_x\""), out _).ShouldBeFalse();

    [Fact]
    public void Should_ParseGuid_When_Guid()
    {
        var id = Guid.NewGuid();
        JsonValues.TryGetGuid(Parse($"\"{id}\""), out var parsed).ShouldBeTrue();
        parsed.ShouldBe(id);
    }

    [Theory]
    [InlineData("12", true)]
    [InlineData("1.5", false)]
    [InlineData("9007199254740992", false)]
    [InlineData("\"1\"", false)]
    public void Should_CheckSafeInteger(string json, bool expected) =>
        JsonValues.TryGetSafeInteger(Parse(json), out _).ShouldBe(expected);

    [Fact]
    public void Should_TrimText_When_StringHasPadding()
    {
        JsonValues.TryGetOptionalText(Parse("\"  a  \""), out var text).ShouldBeTrue();
        text.ShouldBe("a");
    }

    [Theory]
    [InlineData("\"   \"")]
    [InlineData("null")]
    public void Should_ReturnNull_When_TextBlank(string json)
    {
        JsonValues.TryGetOptionalText(Parse(json), out var text).ShouldBeTrue();
        text.ShouldBeNull();
    }

    [Fact]
    public void Should_ReturnNull_When_TextMissing()
    {
        JsonValues.TryGetOptionalText(null, out var text).ShouldBeTrue();
        text.ShouldBeNull();
    }

    [Theory]
    [InlineData("5")]
    [InlineData("\"a\\u0000\"")]
    public void Should_RejectText_When_NotPlainString(string json) =>
        JsonValues.TryGetOptionalText(Parse(json), out _).ShouldBeFalse();
}
