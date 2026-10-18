using Mita.Api.Features.Auth.Login;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Auth;

public sealed class LoginValidatorTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", "")]
    public void Should_RequireEmailAndPassword_When_Missing(string? email, string? password)
    {
        var result = new LoginValidator().Validate(new LoginCommand(email, password));

        result.Errors.Select(error => (error.PropertyName, error.ErrorCode)).ShouldBe(
            [("Email", "validation.required"), ("Password", "validation.required")],
            ignoreOrder: true);
    }

    [Fact]
    public void Should_Pass_When_FieldsPresent() =>
        new LoginValidator().Validate(new LoginCommand("a@b", "x")).IsValid.ShouldBeTrue();

    [Fact]
    public void Should_NotPrintPassword_When_CommandFormatted() =>
        new LoginCommand("a@example.com", "hunter2-password").ToString().ShouldNotContain("hunter2");
}
