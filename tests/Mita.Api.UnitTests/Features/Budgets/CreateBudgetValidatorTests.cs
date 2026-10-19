using System.Text.Json;
using Mita.Api.Features.Budgets.CreateBudget;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Budgets;

public sealed class CreateBudgetValidatorTests
{
    private const string Valid =
        """{"categoryId":"0199f1c2-0000-7000-8000-000000000001","period":"2026-10","limit":100000}""";

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "period", "null", "validation.required" },
        { "period", "\"  \"", "validation.required" },
        { "period", "\"2026-13\"", "validation.invalidValue" },
        { "period", "\"2026-10-01\"", "validation.invalidValue" },
        { "period", "202610", "validation.invalidValue" },
        { "categoryId", "null", "validation.required" },
        { "categoryId", "\"cat_x\"", "validation.unknownCategory" },
        { "categoryId", "42", "validation.unknownCategory" },
        { "limit", "null", "validation.required" },
        { "limit", "0", "validation.positiveAmount" },
        { "limit", "-100", "validation.positiveAmount" },
        { "limit", "10.5", "validation.invalidValue" },
        { "limit", "\"100\"", "validation.invalidValue" },
        { "limit", "100000000001", "validation.invalidValue" }
    };

    [Fact]
    public void Should_Pass_When_AllFieldsValid() => Errors(Valid).ShouldBeEmpty();

    [Fact]
    public void Should_ReportRequired_When_BodyIsEmpty() =>
        Errors("{}").OrderBy(error => error.Item1).ShouldBe(
            [("CategoryId", "validation.required"), ("Limit", "validation.required"), ("Period", "validation.required")]);

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Should_ReportOneCode_When_FieldInvalid(string field, string json, string code) =>
        Errors(With(field, json)).ShouldBe([(Pascal(field), code)]);

    [Theory]
    [InlineData("limit", "100000000000")]
    [InlineData("limit", "5.0")]
    [InlineData("period", "\"1970-01\"")]
    public void Should_Accept_When_FieldValid(string field, string json) => Errors(With(field, json)).ShouldBeEmpty();

    private static string Pascal(string field) => char.ToUpperInvariant(field[0]) + field[1..];

    private static string With(string field, string json)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Valid)!;
        body[field] = JsonDocument.Parse(json).RootElement.Clone();
        return JsonSerializer.Serialize(body);
    }

    private static (string, string)[] Errors(string json)
    {
        var request = JsonSerializer.Deserialize<CreateBudgetRequest>(json, JsonSerializerOptions.Web)!;
        var command = new CreateBudgetCommand(request.CategoryId, request.Period, request.Limit);
        return [.. new CreateBudgetValidator().Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }
}
