using System.Text.Json;
using Mita.Api.Features.Accounts.UpdateAccount;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Accounts;

public sealed class UpdateAccountValidatorTests
{
    [Fact]
    public void Should_Pass_When_AllFieldsValid() => Errors(CreateAccountValidatorTests.Valid).ShouldBeEmpty();

    [Fact]
    public void Should_Pass_When_ArchivedOmitted() =>
        Errors(CreateAccountValidatorTests.Without("archived")).ShouldBeEmpty();

    [Theory]
    [MemberData(nameof(CreateAccountValidatorTests.InvalidFields), MemberType = typeof(CreateAccountValidatorTests))]
    public void Should_ReportOneCode_When_FieldInvalid(string field, string json, string code) =>
        Errors(CreateAccountValidatorTests.With(field, json))
            .ShouldBe([(CreateAccountValidatorTests.Pascal(field), code)]);

    [Theory]
    [MemberData(nameof(CreateAccountValidatorTests.ValidFields), MemberType = typeof(CreateAccountValidatorTests))]
    public void Should_Accept_When_FieldValid(string field, string json) =>
        Errors(CreateAccountValidatorTests.With(field, json)).ShouldBeEmpty();

    private static (string, string)[] Errors(string json)
    {
        var request = JsonSerializer.Deserialize<UpdateAccountRequest>(json, JsonSerializerOptions.Web)!;
        var command = new UpdateAccountCommand(
            Guid.NewGuid(),
            request.Name,
            request.Type,
            request.Currency,
            request.OpeningBalance,
            request.CardLast4,
            request.Archived);
        return [.. new UpdateAccountValidator().Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }
}
