using System.Text.Json;
using Mita.Api.Features.Transfers.CreateTransfer;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Transfers;

public sealed class CreateTransferValidatorTests
{
    private const string Valid =
        """
        {"date":"2026-10-18","fromAccountId":"0199f1c2-0000-7000-8000-000000000001",
         "toAccountId":"0199f1c2-0000-7000-8000-000000000002","fromAmount":1000,"rate":4.5,"note":"Rent"}
        """;

    private static readonly DateTimeOffset Now = new(2026, 10, 18, 23, 30, 0, TimeSpan.Zero);

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "date", "null", "validation.required" },
        { "date", "\"  \"", "validation.required" },
        { "date", "\"2026-02-30\"", "validation.invalidValue" },
        { "date", "\"18.10.2026\"", "validation.invalidValue" },
        { "date", "20261018", "validation.invalidValue" },
        { "date", "\"2027-10-19\"", "validation.dateTooFar" },
        { "fromAmount", "null", "validation.required" },
        { "fromAmount", "0", "validation.positiveAmount" },
        { "fromAmount", "-5", "validation.positiveAmount" },
        { "fromAmount", "1.5", "validation.invalidValue" },
        { "fromAmount", "\"10\"", "validation.invalidValue" },
        { "fromAmount", "100000000001", "validation.invalidValue" },
        { "rate", "0", "validation.invalidValue" },
        { "rate", "-1", "validation.invalidValue" },
        { "rate", "\"4\"", "validation.invalidValue" },
        { "rate", "1000000001", "validation.invalidValue" },
        { "rate", "1e300", "validation.invalidValue" },
        { "rate", "true", "validation.invalidValue" },
        { "note", "\"" + new string('a', 201) + "\"", "validation.invalidValue" },
        { "note", "5", "validation.invalidValue" },
        { "note", "\"a\\u0000\"", "validation.invalidValue" },
        { "fromAccountId", "null", "validation.required" },
        { "fromAccountId", "\"acc_x\"", "validation.unknownAccount" },
        { "fromAccountId", "42", "validation.unknownAccount" },
        { "toAccountId", "\"\"", "validation.required" },
        { "toAccountId", "\"acc_y\"", "validation.unknownAccount" }
    };

    public static TheoryData<string, string> ValidFields => new()
    {
        { "date", "\"2027-10-18\"" },
        { "date", "\"2020-01-01\"" },
        { "fromAmount", "100000000000" },
        { "fromAmount", "5.0" },
        { "rate", "1e-9" },
        { "rate", "1000000000" },
        { "rate", "null" },
        { "note", "\"  " + new string('a', 200) + "  \"" },
        { "note", "\"   \"" },
        { "note", "null" }
    };

    [Fact]
    public void Should_Pass_When_AllFieldsValid() => Errors(Valid).ShouldBeEmpty();

    [Fact]
    public void Should_Pass_When_OptionalFieldsAbsent() => Errors(Without(Without(Valid, "rate"), "note")).ShouldBeEmpty();

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Should_ReportOneCode_When_FieldInvalid(string field, string json, string code) =>
        Errors(With(field, json)).ShouldBe([(Pascal(field), code)]);

    [Theory]
    [MemberData(nameof(ValidFields))]
    public void Should_Accept_When_FieldValid(string field, string json) =>
        Errors(With(field, json)).ShouldBeEmpty();

    [Theory]
    [InlineData("date")]
    [InlineData("fromAccountId")]
    [InlineData("toAccountId")]
    [InlineData("fromAmount")]
    public void Should_ReportRequired_When_FieldAbsent(string field) =>
        Errors(Without(Valid, field)).ShouldBe([(Pascal(field), "validation.required")]);

    private static string Pascal(string field) => char.ToUpperInvariant(field[0]) + field[1..];

    private static string With(string field, string json)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Valid)!;
        body[field] = JsonDocument.Parse(json).RootElement.Clone();
        return JsonSerializer.Serialize(body);
    }

    private static string Without(string source, string field)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(source)!;
        body.Remove(field);
        return JsonSerializer.Serialize(body);
    }

    private static (string, string)[] Errors(string json)
    {
        var request = JsonSerializer.Deserialize<CreateTransferRequest>(json, JsonSerializerOptions.Web)!;
        var command = new CreateTransferCommand(
            request.Date, request.FromAccountId, request.ToAccountId, request.FromAmount, request.Rate, request.Note);
        var validator = new CreateTransferValidator(new FixedTimeProvider(Now));
        return [.. validator.Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
