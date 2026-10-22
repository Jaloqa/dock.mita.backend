using System.Text.Json;
using Mita.Api.Features.Debts.CreateDebt;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Debts;

public sealed class CreateDebtValidatorTests
{
    private const string Valid =
        """
        {"direction":"lent","counterparty":"Max","amount":50000,"currency":"PLN","date":"2026-10-18",
         "dueDate":"2026-10-28","accountId":"0199f1c2-0000-7000-8000-000000000002","rate":4.5,"note":"Trip"}
        """;

    private static readonly DateTimeOffset Now = new(2026, 10, 18, 23, 30, 0, TimeSpan.Zero);

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "direction", "null", "validation.required" },
        { "direction", "\" \"", "validation.required" },
        { "direction", "\"gift\"", "validation.invalidValue" },
        { "direction", "\"Lent\"", "validation.invalidValue" },
        { "direction", "1", "validation.invalidValue" },
        { "counterparty", "null", "validation.required" },
        { "counterparty", "\"   \"", "validation.required" },
        { "counterparty", "\"Ma\\u0007x\"", "validation.invalidValue" },
        { "counterparty", "5", "validation.invalidValue" },
        { "counterparty", "\"" + new string('a', 41) + "\"", "validation.nameTooLong" },
        { "amount", "null", "validation.required" },
        { "amount", "0", "validation.positiveAmount" },
        { "amount", "-5", "validation.positiveAmount" },
        { "amount", "1.5", "validation.invalidValue" },
        { "amount", "\"10\"", "validation.invalidValue" },
        { "amount", "100000000001", "validation.invalidValue" },
        { "currency", "null", "validation.required" },
        { "currency", "\"XDR\"", "validation.invalidValue" },
        { "currency", "\"pln\"", "validation.invalidValue" },
        { "date", "null", "validation.required" },
        { "date", "\"2026-02-30\"", "validation.invalidValue" },
        { "date", "20261018", "validation.invalidValue" },
        { "dueDate", "\"x\"", "validation.invalidValue" },
        { "dueDate", "5", "validation.invalidValue" },
        { "dueDate", "\"2026-10-17\"", "validation.dueBeforeDate" },
        { "accountId", "\"1\"", "validation.unknownAccount" },
        { "accountId", "42", "validation.unknownAccount" },
        { "rate", "0", "validation.invalidValue" },
        { "rate", "-1", "validation.invalidValue" },
        { "rate", "\"4\"", "validation.invalidValue" },
        { "rate", "1000000001", "validation.invalidValue" },
        { "note", "5", "validation.invalidValue" },
        { "note", "\"a\\u0000\"", "validation.invalidValue" },
        { "note", "\"" + new string('a', 201) + "\"", "validation.noteTooLong" }
    };

    public static TheoryData<string, string> ValidFields => new()
    {
        { "direction", "\"borrowed\"" },
        { "counterparty", "\"" + new string('a', 40) + "\"" },
        { "counterparty", "\"  " + new string('a', 40) + "  \"" },
        { "amount", "100000000000" },
        { "amount", "5.0" },
        { "currency", "\"KWD\"" },
        { "dueDate", "\"2026-10-18\"" },
        { "dueDate", "null" },
        { "dueDate", "\"\"" },
        { "accountId", "null" },
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
    public void Should_Pass_When_OptionalFieldsAbsent() =>
        Errors(Without(Without(Without(Without(Valid, "dueDate"), "accountId"), "rate"), "note")).ShouldBeEmpty();

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Should_ReportOneCode_When_FieldInvalid(string field, string json, string code) =>
        Errors(With(field, json)).ShouldBe([(Pascal(field), code)]);

    [Theory]
    [MemberData(nameof(ValidFields))]
    public void Should_Accept_When_FieldValid(string field, string json) =>
        Errors(With(field, json)).ShouldBeEmpty();

    [Theory]
    [InlineData("direction")]
    [InlineData("counterparty")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("date")]
    public void Should_ReportRequired_When_FieldAbsent(string field) =>
        Errors(Without(Valid, field)).ShouldBe([(Pascal(field), "validation.required")]);

    [Fact]
    public void Should_Accept_When_DateIsOneYearAhead() =>
        Errors(Without(With("date", "\"2027-10-18\""), "dueDate")).ShouldBeEmpty();

    [Fact]
    public void Should_ReportDateTooFar_When_DateIsMoreThanOneYearAhead() =>
        Errors(Without(With("date", "\"2027-10-19\""), "dueDate")).ShouldBe([("Date", "validation.dateTooFar")]);

    [Fact]
    public void Should_ReportDueBeforeDate_When_DateIsTooFarAndDueDateEarlier() =>
        Errors(With("date", "\"2027-10-19\"")).ShouldBe(
            [("Date", "validation.dateTooFar"), ("DueDate", "validation.dueBeforeDate")],
            ignoreOrder: true);

    [Fact]
    public void Should_SkipDueDateComparison_When_DateInvalid() =>
        Errors(With("date", "\"x\"")).ShouldBe([("Date", "validation.invalidValue")]);

    [Fact]
    public void Should_ReportEveryInvalidField_When_SeveralInvalid() =>
        Errors(With("direction", "\"gift\"").Replace("\"Max\"", "\"\"", StringComparison.Ordinal))
            .ShouldBe([("Direction", "validation.invalidValue"), ("Counterparty", "validation.required")], ignoreOrder: true);

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
        var request = JsonSerializer.Deserialize<CreateDebtRequest>(json, JsonSerializerOptions.Web)!;
        var command = new CreateDebtCommand(
            request.Direction,
            request.Counterparty,
            request.Amount,
            request.Currency,
            request.Date,
            request.DueDate,
            request.AccountId,
            request.Rate,
            request.Note);
        var validator = new CreateDebtValidator(new FixedTimeProvider(Now));
        return [.. validator.Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
