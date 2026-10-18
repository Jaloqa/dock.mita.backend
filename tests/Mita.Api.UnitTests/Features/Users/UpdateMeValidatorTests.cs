using Mita.Api.Features.Users.UpdateMe;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Users;

public sealed class UpdateMeValidatorTests
{
    private const string ValidName = "Anna Nowak";
    private const string ValidEmail = "anna@demo.mita.app";

    public static TheoryData<string?, string?> InvalidNames => new()
    {
        { null, "validation.required" },
        { "", "validation.required" },
        { "   ", "validation.required" },
        { " \t\n", "validation.required" },
        { new string('x', 41), "validation.nameTooLong" },
        { string.Concat(Enumerable.Repeat("😀", 21)), "validation.nameTooLong" },
        { "Anna\0Nowak", "validation.invalidValue" },
        { "Anna\tNowak", "validation.invalidValue" },
        { "An\u0085na", "validation.invalidValue" },
        { "\u001BAnna", "validation.invalidValue" }
    };

    public static TheoryData<string> ValidNames => new()
    {
        "Anna",
        "  Anna Nowak  ",
        new string('a', 40),
        " " + new string('a', 40) + "\t",
        string.Concat(Enumerable.Repeat("😀", 20))
    };

    public static TheoryData<string?, string?> InvalidEmails => new()
    {
        { null, "validation.required" },
        { "", "validation.required" },
        { "   ", "validation.required" },
        { "anna@", "validation.invalidEmail" },
        { "@demo.pl", "validation.invalidEmail" },
        { "anna@demo", "validation.invalidEmail" },
        { "anna@demo.", "validation.invalidEmail" },
        { "an na@demo.pl", "validation.invalidEmail" },
        { "anna nowak@demo.mita.app", "validation.invalidEmail" },
        { "anna@@demo.pl", "validation.invalidEmail" },
        { new string('a', 245) + "@demo.mita", "validation.invalidEmail" },
        { Email(255), "validation.invalidEmail" },
        { "anna\0@demo.pl", "validation.invalidEmail" },
        { "anna@demo.pl\0", "validation.invalidEmail" },
        { "anna@de\u0001mo.pl", "validation.invalidEmail" },
        { "anna@demo.p\u007Fl", "validation.invalidEmail" }
    };

    public static TheoryData<string> ValidEmails => new()
    {
        ValidEmail,
        " ANNA@Demo.Mita.App ",
        "a@b.c",
        "anna@demo..pl",
        "anna.nowak+budget@demo.pl",
        Email(254),
        "  " + Email(254) + "  "
    };

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void Should_ReportOneNameCode_When_NameInvalid(string? name, string? code) =>
        Errors(name, ValidEmail).ShouldBe([("Name", code!)]);

    [Theory]
    [MemberData(nameof(ValidNames))]
    public void Should_AcceptName_When_TrimmedNameFits(string name) =>
        Errors(name, ValidEmail).ShouldBeEmpty();

    [Theory]
    [MemberData(nameof(InvalidEmails))]
    public void Should_ReportOneEmailCode_When_EmailInvalid(string? email, string? code) =>
        Errors(ValidName, email).ShouldBe([("Email", code!)]);

    [Theory]
    [MemberData(nameof(ValidEmails))]
    public void Should_AcceptEmail_When_TrimmedEmailValid(string email) =>
        Errors(ValidName, email).ShouldBeEmpty();

    [Fact]
    public void Should_ReportBothFields_When_BothMissing() =>
        Errors(null, null).ShouldBe(
            [("Name", "validation.required"), ("Email", "validation.required")],
            ignoreOrder: true);

    [Fact]
    public void Should_NotPrintName_When_CommandFormatted() =>
        new UpdateMeCommand("Secret Person", ValidEmail).ToString().ShouldNotContain("Secret Person");

    private static (string, string)[] Errors(string? name, string? email) =>
        [.. new UpdateMeValidator().Validate(new UpdateMeCommand(name, email)).Errors
            .Select(error => (error.PropertyName, error.ErrorCode))];

    private static string Email(int length) => new string('a', length - "@demo.pl".Length) + "@demo.pl";
}
