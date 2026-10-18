using Mita.Api.Features.Users.DeleteMe;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Users;

public sealed class DeleteMeValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Should_RequirePassword_When_Missing(string? password) =>
        new DeleteMeValidator().Validate(new DeleteMeCommand(password)).Errors
            .Select(error => (error.PropertyName, error.ErrorCode))
            .ShouldBe([("Password", "validation.required")]);

    [Fact]
    public void Should_Pass_When_PasswordPresent() =>
        new DeleteMeValidator().Validate(new DeleteMeCommand("any-password")).IsValid.ShouldBeTrue();

    [Fact]
    public void Should_NotPrintPassword_When_CommandFormatted() =>
        new DeleteMeCommand("hunter2-password").ToString().ShouldNotContain("hunter2");
}
