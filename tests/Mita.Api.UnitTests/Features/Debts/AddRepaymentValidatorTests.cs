using System.Text.Json;
using Mita.Api.Features.Debts.AddRepayment;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Debts;

public sealed class AddRepaymentValidatorTests
{
    private const string Valid =
        """
        {"date":"2026-10-18","amount":50000,"currency":"EUR","rate":4.5,
         "accountId":"0199f1c2-0000-7000-8000-000000000002","note":"Part one"}
        """;

    private static readonly DateTimeOffset Now = new(2026, 10, 18, 23, 30, 0, TimeSpan.Zero);

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "amount", "null", "validation.required" },
        { "amount", "\" \"", "validation.required" },
        { "amount", "0", "validation.positiveAmount" },
        { "amount", "-5", "validation.positiveAmount" },
        { "amount", "1.5", "validation.invalidValue" },
        { "amount", "\"10\"", "validation.invalidValue" },
        { "amount", "100000000001", "validation.invalidValue" },
        { "currency", "null", "validation.required" },
        { "currency", "\"XDR\"", "validation.invalidValue" },
        { "currency", "\"pln\"", "validation.invalidValue" },
        { "currency", "5", "validation.invalidValue" },
        { "date", "null", "validation.required" },
        { "date", "\"  \"", "validation.required" },
        { "date", "\"2026-02-30\"", "validation.invalidValue" },
        { "date", "20261018", "validation.invalidValue" },
        { "date", "\"2027-10-19\"", "validation.dateTooFar" },
        { "rate", "0", "validation.invalidValue" },
        { "rate", "-1", "validation.invalidValue" },
        { "rate", "\"4\"", "validation.invalidValue" },
        { "rate", "1000000001", "validation.invalidValue" },
        { "accountId", "\"1\"", "validation.unknownAccount" },
        { "accountId", "42", "validation.unknownAccount" },
        { "note", "5", "validation.invalidValue" },
        { "note", "\"a\\u0000\"", "validation.invalidValue" },
        { "note", "\"" + new string('a', 201) + "\"", "validation.noteTooLong" }
    };

    public static TheoryData<string, string> ValidFields => new()
    {
        { "amount", "100000000000" },
        { "amount", "5.0" },
        { "currency", "\"KWD\"" },
        { "date", "\"2027-10-18\"" },
        { "rate", "1e-9" },
        { "rate", "1000000000" },
        { "rate", "null" },
        { "accountId", "null" },
        { "accountId", "\"  \"" },
        { "note", "\"  " + new string('a', 200) + "  \"" },
        { "note", "\"   \"" },
        { "note", "null" }
    };

    [Fact]
    public void Should_Pass_When_AllFieldsValid() => Errors(Valid).ShouldBeEmpty();

    [Fact]
    public void Should_Pass_When_OptionalFieldsAbsent() =>
        Errors(Without(Without(Without(Valid, "rate"), "accountId"), "note")).ShouldBeEmpty();

    [Fact]
    public void Should_ReportRequired_When_BodyIsEmpty() =>
        Errors("{}").OrderBy(error => error.Item1).ShouldBe(
            [("Amount", "validation.required"), ("Currency", "validation.required"), ("Date", "validation.required")]);

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Should_ReportOneCode_When_FieldInvalid(string field, string json, string code) =>
        Errors(With(field, json)).ShouldBe([(Pascal(field), code)]);

    [Theory]
    [MemberData(nameof(ValidFields))]
    public void Should_Accept_When_FieldValid(string field, string json) =>
        Errors(With(field, json)).ShouldBeEmpty();

    [Theory]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("date")]
    public void Should_ReportRequired_When_FieldAbsent(string field) =>
        Errors(Without(Valid, field)).ShouldBe([(Pascal(field), "validation.required")]);

    [Fact]
    public void Should_Accept_When_DateIsOneYearAhead() =>
        Errors(With("date", "\"2027-10-18\"")).ShouldBeEmpty();

    [Fact]
    public void Should_ReportEveryInvalidField_When_SeveralInvalid() =>
        Errors(With("amount", "0").Replace("\"EUR\"", "\"XDR\"", StringComparison.Ordinal))
            .ShouldBe([("Amount", "validation.positiveAmount"), ("Currency", "validation.invalidValue")], ignoreOrder: true);

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
        var request = JsonSerializer.Deserialize<AddRepaymentRequest>(json, JsonSerializerOptions.Web)!;
        var command = new AddRepaymentCommand(
            Guid.NewGuid(),
            request.Date,
            request.Amount,
            request.Currency,
            request.Rate,
            request.AccountId,
            request.Note);
        var validator = new AddRepaymentValidator(new FixedTimeProvider(Now));
        return [.. validator.Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
