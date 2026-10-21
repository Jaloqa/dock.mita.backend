using System.Text.Json;
using Mita.Api.Common.Tags;
using Shouldly;

namespace Mita.Api.UnitTests.Common;

public sealed class TagNamesTests
{
    private static JsonElement? Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static string Array(params string[] names) => JsonSerializer.Serialize(names);

    private static string Long(int length) => new('a', length);

    [Fact]
    public void Should_BeValid_When_TagsMissing() => TagNames.Error(null).ShouldBeNull();

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public void Should_BeValid_When_TagsNullOrEmpty(string json) => TagNames.Error(Parse(json)).ShouldBeNull();

    [Theory]
    [InlineData("\"a\"")]
    [InlineData("{}")]
    [InlineData("[5]")]
    [InlineData("[\"  \"]")]
    [InlineData("[\"a\\u0007\"]")]
    public void Should_ReturnInvalidValue_When_TagsMalformed(string json) =>
        TagNames.Error(Parse(json)).ShouldBe("validation.invalidValue");

    [Fact]
    public void Should_ReturnInvalidValue_When_FirstRuleAlsoFailsLaterRule() =>
        TagNames.Error(Parse($"[5, \"{Long(31)}\"]")).ShouldBe("validation.invalidValue");

    [Fact]
    public void Should_ReturnTagTooLong_When_NameOver30() =>
        TagNames.Error(Parse(Array(Long(31)))).ShouldBe("validation.tagTooLong");

    [Fact]
    public void Should_BeValid_When_NameIs30() => TagNames.Error(Parse(Array(Long(30)))).ShouldBeNull();

    [Fact]
    public void Should_ReturnTooManyTags_When_MoreThan10Distinct()
    {
        var names = Enumerable.Range(0, 11).Select(i => $"tag{i}").ToArray();

        TagNames.Error(Parse(Array(names))).ShouldBe("validation.tooManyTags");
    }

    [Fact]
    public void Should_BeValid_When_10DistinctAmongDuplicates()
    {
        var names = Enumerable.Range(0, 9).Select(i => $"tag{i}").Concat(["a", "A", " a "]).ToArray();

        TagNames.Error(Parse(Array(names))).ShouldBeNull();
    }

    [Fact]
    public void Should_TrimAndDedupeKeepingFirstSpelling_When_Parsing() =>
        TagNames.Parse(Parse("[\" Trip \", \"trip\", \"Food\"]")).ShouldBe(["Trip", "Food"]);

    [Fact]
    public void Should_ReturnEmpty_When_ParsingMissing() => TagNames.Parse(null).ShouldBeEmpty();

    [Fact]
    public void Should_Throw_When_ParsingInvalid() =>
        Should.Throw<InvalidOperationException>(() => TagNames.Parse(Parse("\"a\"")));

    [Fact]
    public void Should_ReturnRequired_When_NameMissing() => TagNames.NameError(null).ShouldBe("validation.required");

    [Fact]
    public void Should_ReturnInvalidValue_When_NameNotString() =>
        TagNames.NameError(Parse("5")).ShouldBe("validation.invalidValue");

    [Fact]
    public void Should_ReturnInvalidValue_When_NameHasControlCharacter() =>
        TagNames.NameError(Parse("\"a\\u0000\"")).ShouldBe("validation.invalidValue");

    [Fact]
    public void Should_ReturnTagTooLong_When_RenameOver30() =>
        TagNames.NameError(Parse($"\"{Long(31)}\"")).ShouldBe("validation.tagTooLong");

    [Fact]
    public void Should_ParseTrimmedName_When_NameValid()
    {
        TagNames.NameError(Parse("\"  Trip \"")).ShouldBeNull();
        TagNames.TryParseName(Parse("\"  Trip \""), out var name).ShouldBeTrue();
        name.ShouldBe("Trip");
    }
}
