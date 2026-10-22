using System.Text.Json;
using Mita.Api.Features.Transactions.CreateTransaction;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Transactions;

public sealed class CreateTransactionValidatorTests
{
    private const string Valid =
        """
        {"kind":"expense","date":"2026-10-18","title":"Bread","note":"Bakery",
         "categoryId":"0199f1c2-0000-7000-8000-000000000001","accountId":"0199f1c2-0000-7000-8000-000000000002",
         "amount":1250,"currency":"EUR","rate":4.5}
        """;

    private static readonly DateTimeOffset Now = new(2026, 10, 18, 23, 30, 0, TimeSpan.Zero);

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "kind", "null", "validation.required" },
        { "kind", "\" \"", "validation.required" },
        { "kind", "\"transfer\"", "validation.invalidValue" },
        { "kind", "\"Expense\"", "validation.invalidValue" },
        { "kind", "1", "validation.invalidValue" },
        { "date", "null", "validation.required" },
        { "date", "\"2026-02-30\"", "validation.invalidValue" },
        { "date", "20261018", "validation.invalidValue" },
        { "date", "\"2027-10-19\"", "validation.dateTooFar" },
        { "title", "5", "validation.invalidValue" },
        { "title", "\"" + new string('a', 101) + "\"", "validation.titleTooLong" },
        { "title", "\"a\\u0007\"", "validation.invalidValue" },
        { "note", "5", "validation.invalidValue" },
        { "note", "\"" + new string('a', 201) + "\"", "validation.noteTooLong" },
        { "note", "\"a\\u0000\"", "validation.invalidValue" },
        { "categoryId", "null", "validation.required" },
        { "categoryId", "\"cat_x\"", "validation.unknownCategory" },
        { "categoryId", "42", "validation.unknownCategory" },
        { "accountId", "\"\"", "validation.required" },
        { "accountId", "\"acc_x\"", "validation.unknownAccount" },
        { "amount", "null", "validation.required" },
        { "amount", "0", "validation.positiveAmount" },
        { "amount", "-5", "validation.positiveAmount" },
        { "amount", "1.5", "validation.invalidValue" },
        { "amount", "\"10\"", "validation.invalidValue" },
        { "amount", "100000000001", "validation.invalidValue" },
        { "currency", "null", "validation.required" },
        { "currency", "\"XDR\"", "validation.invalidValue" },
        { "currency", "\"pln\"", "validation.invalidValue" },
        { "rate", "0", "validation.invalidValue" },
        { "rate", "-1", "validation.invalidValue" },
        { "rate", "\"4\"", "validation.invalidValue" },
        { "rate", "1000001", "validation.invalidValue" },
        { "rate", "true", "validation.invalidValue" },
        { "tags", "\"a\"", "validation.invalidValue" },
        { "tags", "[\"" + new string('a', 31) + "\"]", "validation.tagTooLong" },
        { "tags", "[\"1\",\"2\",\"3\",\"4\",\"5\",\"6\",\"7\",\"8\",\"9\",\"10\",\"11\"]", "validation.tooManyTags" }
    };

    public static TheoryData<string, string> ValidFields => new()
    {
        { "kind", "\"income\"" },
        { "date", "\"2027-10-18\"" },
        { "title", "\"  " + new string('a', 100) + "  \"" },
        { "title", "\"   \"" },
        { "title", "null" },
        { "note", "\"  " + new string('a', 200) + "  \"" },
        { "note", "\"   \"" },
        { "note", "null" },
        { "amount", "100000000000" },
        { "amount", "5.0" },
        { "currency", "\"PLN\"" },
        { "rate", "1e-9" },
        { "rate", "1000000" },
        { "rate", "null" },
        { "tags", "null" },
        { "tags", "[]" }
    };

    [Fact]
    public void Should_Pass_When_AllFieldsValid() => Errors(Valid).ShouldBeEmpty();

    [Fact]
    public void Should_Pass_When_OptionalFieldsAbsent() =>
        Errors(Without(Without(Without(Without(Valid, "rate"), "note"), "title"), "tags")).ShouldBeEmpty();

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Should_ReportOneCode_When_FieldInvalid(string field, string json, string code) =>
        Errors(With(field, json)).ShouldBe([(Pascal(field), code)]);

    [Theory]
    [MemberData(nameof(ValidFields))]
    public void Should_Accept_When_FieldValid(string field, string json) =>
        Errors(With(field, json)).ShouldBeEmpty();

    [Theory]
    [InlineData("kind")]
    [InlineData("date")]
    [InlineData("categoryId")]
    [InlineData("accountId")]
    [InlineData("amount")]
    [InlineData("currency")]
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
        var request = JsonSerializer.Deserialize<CreateTransactionRequest>(json, JsonSerializerOptions.Web)!;
        var command = new CreateTransactionCommand(
            request.Kind,
            request.Date,
            request.Title,
            request.Note,
            request.CategoryId,
            request.AccountId,
            request.Amount,
            request.Currency,
            request.Rate,
            request.Tags);
        var validator = new CreateTransactionValidator(new FixedTimeProvider(Now));
        return [.. validator.Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
