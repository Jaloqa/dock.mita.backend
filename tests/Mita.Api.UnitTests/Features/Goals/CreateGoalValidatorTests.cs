using System.Text.Json;
using Mita.Api.Features.Goals.CreateGoal;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Goals;

public sealed class CreateGoalValidatorTests
{
    private const string Valid =
        """{"name":"Wakacje","target":800000,"deadline":"2027-06","accountId":"0199f1c2-0000-7000-8000-000000000001"}""";

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "name", "null", "validation.required" },
        { "name", "\"   \"", "validation.required" },
        { "name", "42", "validation.invalidValue" },
        { "name", $"\"{new string('x', 41)}\"", "validation.nameTooLong" },
        { "name", "\"a\\u0007\"", "validation.invalidValue" },
        { "target", "null", "validation.required" },
        { "target", "0", "validation.positiveAmount" },
        { "target", "-5", "validation.positiveAmount" },
        { "target", "10.5", "validation.invalidValue" },
        { "target", "\"100\"", "validation.invalidValue" },
        { "target", "100000000001", "validation.invalidValue" },
        { "deadline", "\"2026-13\"", "validation.invalidValue" },
        { "deadline", "\"2026-10-01\"", "validation.invalidValue" },
        { "deadline", "202610", "validation.invalidValue" },
        { "accountId", "\"acc_x\"", "validation.unknownAccount" },
        { "accountId", "42", "validation.unknownAccount" }
    };

    [Fact]
    public void Should_Pass_When_AllFieldsValid() => Errors(Valid).ShouldBeEmpty();

    [Fact]
    public void Should_ReportRequired_When_BodyIsEmpty() =>
        Errors("{}").OrderBy(error => error.Item1).ShouldBe(
            [("Name", "validation.required"), ("Target", "validation.required")]);

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Should_ReportOneCode_When_FieldInvalid(string field, string json, string code) =>
        Errors(With(field, json)).ShouldBe([(Pascal(field), code)]);

    [Theory]
    [InlineData("name", "\"  Wakacje  \"")]
    [InlineData("name", "\"  xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx  \"")]
    [InlineData("target", "100000000000")]
    [InlineData("deadline", "null")]
    [InlineData("accountId", "null")]
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
        var request = JsonSerializer.Deserialize<CreateGoalRequest>(json, JsonSerializerOptions.Web)!;
        var command = new CreateGoalCommand(request.Name, request.Target, request.Deadline, request.AccountId);
        return [.. new CreateGoalValidator().Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }
}
