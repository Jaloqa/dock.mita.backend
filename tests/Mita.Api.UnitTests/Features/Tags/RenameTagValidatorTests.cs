using System.Text.Json;
using Mita.Api.Features.Tags.RenameTag;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Tags;

public sealed class RenameTagValidatorTests
{
    public static TheoryData<string, string> InvalidNames => new()
    {
        { "null", "validation.required" },
        { "\"   \"", "validation.required" },
        { "5", "validation.invalidValue" },
        { "[\"a\"]", "validation.invalidValue" },
        { "\"a\\u0007\"", "validation.invalidValue" },
        { "\"  " + new string('a', 31) + "  \"", "validation.tagTooLong" }
    };

    public static TheoryData<string> ValidNames => new()
    {
        "\"trip\"",
        "\"  " + new string('a', 30) + "  \"",
        "\"Road  trip\""
    };

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void Should_ReportOneCode_When_NameInvalid(string json, string code) =>
        Errors(json).ShouldBe([("Name", code)]);

    [Theory]
    [MemberData(nameof(ValidNames))]
    public void Should_Accept_When_NameValid(string json) => Errors(json).ShouldBeEmpty();

    [Fact]
    public void Should_ReportRequired_When_NameAbsent() =>
        Errors(null).ShouldBe([("Name", "validation.required")]);

    private static (string, string)[] Errors(string? json)
    {
        JsonElement? name = json is null ? null : JsonDocument.Parse(json).RootElement.Clone();
        var command = new RenameTagCommand(Guid.NewGuid(), name);
        return [.. new RenameTagValidator().Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }
}
