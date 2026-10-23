using System.Text.Json;
using Mita.Api.Features.Groups.InviteMember;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Groups;

public sealed class InviteMemberValidatorTests
{
    public static TheoryData<string, string> InvalidEmails => new()
    {
        { "{}", "validation.required" },
        { """{"email":null}""", "validation.required" },
        { """{"email":" "}""", "validation.required" },
        { """{"email":42}""", "validation.invalidEmail" },
        { """{"email":"no-at-sign"}""", "validation.invalidEmail" },
        { """{"email":"max@example"}""", "validation.invalidEmail" },
        { """{"email":"max@exa mple.com"}""", "validation.invalidEmail" },
        { """{"email":["max@example.com"]}""", "validation.invalidEmail" },
        { "{\"email\":\"" + new string('a', 243) + "@example.com\"}", "validation.invalidEmail" },
        { "{\"email\":\"" + new string('a', 241) + "\\u0958@example.com\"}", "validation.invalidEmail" },
        { """{"email":"max\ufffe@example.com"}""", "validation.invalidEmail" }
    };

    public static TheoryData<string> ValidEmails => new()
    {
        """{"email":" Max@Example.com "}""",
        """{"email":"m@x.pl"}""",
        """{"email":"rene@cafe\u0301.fr"}""",
        "{\"email\":\"  " + new string('a', 242) + "@example.com  \"}"
    };

    [Theory]
    [MemberData(nameof(InvalidEmails))]
    public void Should_ReportOneCode_When_EmailInvalid(string json, string code) =>
        Errors(json).ShouldBe([("Email", code)]);

    [Theory]
    [MemberData(nameof(ValidEmails))]
    public void Should_Accept_When_EmailValid(string json) => Errors(json).ShouldBeEmpty();

    private static (string, string)[] Errors(string json)
    {
        var request = JsonSerializer.Deserialize<InviteMemberRequest>(json, JsonSerializerOptions.Web)!;
        var command = new InviteMemberCommand(Guid.NewGuid(), Guid.NewGuid(), request.Email);
        return [.. new InviteMemberValidator().Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }
}
