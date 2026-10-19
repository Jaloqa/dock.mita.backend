using System.Text.Json;
using Mita.Api.Features.Goals.ContributeToGoal;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Goals;

public sealed class ContributeToGoalValidatorTests
{
    private const string Valid =
        """{"amount":120000,"date":"2026-10-18","fromAccountId":"0199f1c2-0000-7000-8000-000000000001","rate":4.5}""";

    private static readonly DateTimeOffset Now = new(2026, 10, 18, 23, 30, 0, TimeSpan.Zero);

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "amount", "null", "validation.required" },
        { "amount", "0", "validation.positiveAmount" },
        { "amount", "-5", "validation.positiveAmount" },
        { "amount", "1.5", "validation.invalidValue" },
        { "amount", "\"100\"", "validation.invalidValue" },
        { "amount", "100000000001", "validation.invalidValue" },
        { "date", "null", "validation.required" },
        { "date", "\"  \"", "validation.required" },
        { "date", "\"2026-02-30\"", "validation.invalidValue" },
        { "date", "20261018", "validation.invalidValue" },
        { "date", "\"2027-10-19\"", "validation.dateTooFar" },
        { "rate", "-1", "validation.invalidValue" },
        { "rate", "0", "validation.invalidValue" },
        { "rate", "\"4\"", "validation.invalidValue" },
        { "rate", "1000001", "validation.invalidValue" },
        { "fromAccountId", "\"acc_x\"", "validation.unknownAccount" },
        { "fromAccountId", "42", "validation.unknownAccount" }
    };

    public static TheoryData<string, string> ValidFields => new()
    {
        { "amount", "100000000000" },
        { "date", "\"2027-10-18\"" },
        { "rate", "1000000" },
        { "rate", "null" },
        { "fromAccountId", "null" },
        { "fromAccountId", "\"  \"" }
    };

    [Fact]
    public void Should_Pass_When_AllFieldsValid() => Errors(Valid).ShouldBeEmpty();

    [Fact]
    public void Should_ReportRequired_When_BodyIsEmpty() =>
        Errors("{}").OrderBy(error => error.Item1).ShouldBe(
            [("Amount", "validation.required"), ("Date", "validation.required")]);

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Should_ReportOneCode_When_FieldInvalid(string field, string json, string code) =>
        Errors(With(field, json)).ShouldBe([(Pascal(field), code)]);

    [Theory]
    [MemberData(nameof(ValidFields))]
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
        var request = JsonSerializer.Deserialize<ContributeToGoalRequest>(json, JsonSerializerOptions.Web)!;
        var command = new ContributeToGoalCommand(Guid.NewGuid(), request.Amount, request.Date, request.FromAccountId, request.Rate);
        var validator = new ContributeToGoalValidator(new FixedTimeProvider(Now));
        return [.. validator.Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
