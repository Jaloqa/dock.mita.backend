using System.Text.Json;
using Mita.Api.Features.Groups.AddMember;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Groups;

public sealed class AddMemberValidatorTests
{
    public static TheoryData<string, string> InvalidNames => new()
    {
        { "{}", "validation.required" },
        { """{"name":null}""", "validation.required" },
        { """{"name":"   "}""", "validation.required" },
        { """{"name":5}""", "validation.invalidValue" },
        { """{"name":"Ma\u0007x"}""", "validation.invalidValue" },
        { "{\"name\":\"" + new string('a', 41) + "\"}", "validation.nameTooLong" }
    };

    public static TheoryData<string> ValidNames => new()
    {
        """{"name":" Kasia "}""",
        "{\"name\":\"" + new string('a', 40) + "\"}",
        "{\"name\":\"  " + new string('a', 40) + "  \"}"
    };

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void Should_ReportOneCode_When_NameInvalid(string json, string code) =>
        Errors(json).ShouldBe([("Name", code)]);

    [Theory]
    [MemberData(nameof(ValidNames))]
    public void Should_Accept_When_NameValid(string json) => Errors(json).ShouldBeEmpty();

    private static (string, string)[] Errors(string json)
    {
        var request = JsonSerializer.Deserialize<AddMemberRequest>(json, JsonSerializerOptions.Web)!;
        var command = new AddMemberCommand(Guid.NewGuid(), request.Name);
        return [.. new AddMemberValidator().Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }
}
