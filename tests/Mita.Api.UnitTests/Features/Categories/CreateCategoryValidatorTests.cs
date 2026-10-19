using System.Text.Json;
using Mita.Api.Features.Categories.CreateCategory;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Categories;

public sealed class CreateCategoryValidatorTests
{
    private const string Valid = """{"name":"Pets","kind":"expense","color":"chart-3"}""";

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "name", "null", "validation.required" },
        { "name", "\"   \"", "validation.required" },
        { "name", "5", "validation.invalidValue" },
        { "name", "\"  " + new string('a', 41) + "  \"", "validation.nameTooLong" },
        { "name", "\"a\\u0007\"", "validation.invalidValue" },
        { "kind", "null", "validation.required" },
        { "kind", "\"transfer\"", "validation.invalidValue" },
        { "kind", "\"Expense\"", "validation.invalidValue" },
        { "color", "null", "validation.required" },
        { "color", "\"chart-7\"", "validation.invalidValue" },
        { "color", "\"red\"", "validation.invalidValue" }
    };

    public static TheoryData<string, string> ValidFields => new()
    {
        { "name", "\"  " + new string('a', 40) + "  \"" },
        { "kind", "\"income\"" },
        { "color", "\"chart-1\"" },
        { "color", "\"chart-6\"" }
    };

    [Fact]
    public void Should_Pass_When_AllFieldsValid() => Errors(Valid).ShouldBeEmpty();

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
    [InlineData("kind")]
    [InlineData("color")]
    public void Should_ReportRequired_When_FieldAbsent(string field) =>
        Errors(Without(field)).ShouldBe([(Pascal(field), "validation.required")]);

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
        var request = JsonSerializer.Deserialize<CreateCategoryRequest>(json, JsonSerializerOptions.Web)!;
        var command = new CreateCategoryCommand(request.Name, request.Kind, request.Color);
        return [.. new CreateCategoryValidator().Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }
}
