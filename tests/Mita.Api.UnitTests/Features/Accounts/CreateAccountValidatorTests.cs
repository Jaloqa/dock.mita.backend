using System.Text.Json;
using Mita.Api.Features.Accounts.CreateAccount;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Accounts;

public sealed class CreateAccountValidatorTests
{
    private const string Valid =
        """{"name":"Main","type":"card","currency":"PLN","openingBalance":1000,"cardLast4":"1234","archived":false}""";

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "name", "null", "validation.required" },
        { "name", "\"   \"", "validation.required" },
        { "name", "5", "validation.invalidValue" },
        { "name", "\"" + new string('a', 41) + "\"", "validation.nameTooLong" },
        { "name", "\"a\\u0007\"", "validation.invalidValue" },
        { "type", "null", "validation.required" },
        { "type", "\"bank\"", "validation.invalidValue" },
        { "currency", "\"\"", "validation.required" },
        { "currency", "\"GBP\"", "validation.invalidValue" },
        { "currency", "\"pln\"", "validation.invalidValue" },
        { "openingBalance", "null", "validation.required" },
        { "openingBalance", "\"100\"", "validation.invalidValue" },
        { "openingBalance", "1.5", "validation.invalidValue" },
        { "openingBalance", "100000000001", "validation.invalidValue" },
        { "cardLast4", "\"12a4\"", "validation.cardLast4" },
        { "cardLast4", "\"12345\"", "validation.cardLast4" },
        { "cardLast4", "1234", "validation.cardLast4" },
        { "archived", "\"yes\"", "validation.invalidValue" }
    };

    public static TheoryData<string, string> ValidFields => new()
    {
        { "name", "\"" + new string('a', 40) + "\"" },
        { "name", "\"  Wallet  \"" },
        { "type", "\"cash\"" },
        { "type", "\"savings\"" },
        { "currency", "\"UAH\"" },
        { "openingBalance", "-100000000000" },
        { "openingBalance", "100000000000" },
        { "openingBalance", "5.0" },
        { "openingBalance", "0" },
        { "cardLast4", "null" },
        { "cardLast4", "\"\"" },
        { "archived", "true" },
        { "archived", "null" }
    };

    [Fact]
    public void Should_Pass_When_AllFieldsValid() => Errors(Valid).ShouldBeEmpty();

    [Fact]
    public void Should_Pass_When_OptionalFieldsAbsent() =>
        Errors("""{"name":"Main","type":"cash","currency":"EUR","openingBalance":0}""").ShouldBeEmpty();

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Should_ReportOneCode_When_FieldInvalid(string field, string json, string code) =>
        Errors(With(field, json)).ShouldBe([(Pascal(field), code)]);

    [Theory]
    [MemberData(nameof(ValidFields))]
    public void Should_Accept_When_FieldValid(string field, string json) =>
        Errors(With(field, json)).ShouldBeEmpty();

    [Theory]
    [InlineData("name")]
    [InlineData("type")]
    [InlineData("currency")]
    [InlineData("openingBalance")]
    public void Should_ReportRequired_When_FieldAbsent(string field) =>
        Errors(Without(field)).ShouldBe([(Pascal(field), "validation.required")]);

    [Fact]
    public void Should_ReportEveryRequiredField_When_BodyEmpty() =>
        Errors("{}").Select(error => error.Item1).ShouldBe(
            ["Name", "Type", "Currency", "OpeningBalance"],
            ignoreOrder: true);

    private static string Pascal(string field) => char.ToUpperInvariant(field[0]) + field[1..];

    private static string With(string field, string json)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Valid)!;
        body[field] = JsonDocument.Parse(json).RootElement.Clone();
        return JsonSerializer.Serialize(body);
    }

    private static string Without(string field)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Valid)!;
        body.Remove(field);
        return JsonSerializer.Serialize(body);
    }

    private static (string, string)[] Errors(string json)
    {
        var request = JsonSerializer.Deserialize<CreateAccountRequest>(json, JsonSerializerOptions.Web)!;
        var command = new CreateAccountCommand(
            request.Name, request.Type, request.Currency, request.OpeningBalance, request.CardLast4, request.Archived);
        return [.. new CreateAccountValidator().Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }
}
