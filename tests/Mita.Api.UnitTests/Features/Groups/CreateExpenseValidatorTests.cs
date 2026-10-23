using System.Text.Json;
using Mita.Api.Common.Groups;
using Mita.Api.Features.Groups.CreateExpense;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Groups;

public sealed class CreateExpenseValidatorTests
{
    private const string First = "0199f1c2-0000-7000-8000-000000000001";
    private const string Second = "0199f1c2-0000-7000-8000-000000000002";

    private const string Valid =
        """
        {"payerId":"0199f1c2-0000-7000-8000-000000000001","amount":10000,"currency":"EUR","date":"2026-10-18",
         "categoryId":"groceries","tags":["Beach"],"description":"Dinner","rate":4.5,
         "split":{"mode":"equal","memberIds":["0199f1c2-0000-7000-8000-000000000001","0199f1c2-0000-7000-8000-000000000002"]}}
        """;

    private static readonly DateTimeOffset Now = new(2026, 10, 18, 23, 30, 0, TimeSpan.Zero);

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "payerId", "null", "validation.required" },
        { "payerId", "\"x\"", "validation.unknownMember" },
        { "payerId", "5", "validation.unknownMember" },
        { "amount", "null", "validation.required" },
        { "amount", "1.5", "validation.invalidValue" },
        { "amount", "\"10\"", "validation.invalidValue" },
        { "amount", "100000000001", "validation.invalidValue" },
        { "amount", "0", "validation.positiveAmount" },
        { "amount", "-5", "validation.positiveAmount" },
        { "currency", "null", "validation.required" },
        { "currency", "\"eur\"", "validation.invalidValue" },
        { "currency", "\"XDR\"", "validation.invalidValue" },
        { "date", "null", "validation.required" },
        { "date", "\"2026-02-30\"", "validation.invalidValue" },
        { "date", "20261018", "validation.invalidValue" },
        { "date", "\"2027-10-19\"", "validation.dateTooFar" },
        { "categoryId", "\"food\"", "validation.unknownCategory" },
        { "categoryId", "\"transfer\"", "validation.unknownCategory" },
        { "categoryId", "\"Groceries\"", "validation.unknownCategory" },
        { "categoryId", "5", "validation.unknownCategory" },
        { "tags", "\"Beach\"", "validation.invalidValue" },
        { "tags", Tags(11), "validation.tooManyTags" },
        { "tags", "[\"" + new string('a', 31) + "\"]", "validation.tagTooLong" },
        { "description", "5", "validation.invalidValue" },
        { "description", "\"A\\u0007\"", "validation.invalidValue" },
        { "description", "\"" + new string('a', 101) + "\"", "validation.titleTooLong" },
        { "rate", "0", "validation.invalidValue" },
        { "rate", "\"4\"", "validation.invalidValue" },
        { "rate", "1000000001", "validation.invalidValue" },
        { "split", "\"equal\"", "validation.invalidValue" },
        { "split", """{"mode":"ratio"}""", "validation.invalidValue" },
        { "split", """{"memberIds":["0199f1c2-0000-7000-8000-000000000001"]}""", "validation.invalidValue" },
        { "split", """{"mode":"equal","memberIds":[]}""", "validation.invalidValue" },
        { "split", """{"mode":"equal"}""", "validation.invalidValue" },
        { "split", Equal(First, First), "validation.invalidValue" },
        { "split", Equal(First, First.ToUpperInvariant()), "validation.invalidValue" },
        { "split", Equal([.. Enumerable.Range(1, 51).Select(index => $"0199f1c2-0000-7000-8000-{index:D12}")]), "validation.invalidValue" },
        { "split", Equal("x"), "validation.unknownMember" },
        { "split", """{"mode":"equal","memberIds":[5]}""", "validation.unknownMember" },
        { "split", Exact((First, "4000"), (Second, "5999")), "validation.splitMismatch" },
        { "split", Exact((First, "10000"), (Second, "1")), "validation.splitMismatch" },
        { "split", Exact((First, "0")), "validation.invalidValue" },
        { "split", Exact((First, "1.5"), (Second, "9998.5")), "validation.invalidValue" },
        { "split", Exact((First, "\"10000\"")), "validation.invalidValue" },
        { "split", Exact((First, "5000"), (First, "5000")), "validation.invalidValue" },
        { "split", Exact(("x", "10000")), "validation.unknownMember" },
        { "split", Equal("x", "y"), "validation.unknownMember" },
        { "split", """{"mode":"exact","shares":[{"amount":5000},{"amount":5000}]}""", "validation.unknownMember" },
        { "split", $$"""{"mode":"equal","memberIds":["{{First}}","{{First}}",42]}""", "validation.invalidValue" },
        { "split", Equal(First, "x", First), "validation.invalidValue" },
        { "split", Equal(First, $" {First} "), "validation.invalidValue" },
        { "split", Equal("x", "x"), "validation.invalidValue" },
        { "split", Equal("x", " x "), "validation.invalidValue" },
        { "split", """{"mode":"equal","memberIds":[42,42]}""", "validation.invalidValue" },
        { "split", Exact(("x", "5000"), ("x", "5000")), "validation.invalidValue" },
        {
            "split",
            $$"""{"mode":"exact","shares":[{"memberId":"{{First}}","amount":4000},{"memberId":"{{First}}","amount":3000},{"amount":3000}]}""",
            "validation.invalidValue"
        },
        { "split", """{"mode":"exact","shares":[]}""", "validation.invalidValue" },
        { "split", """{"mode":"exact","shares":[5]}""", "validation.invalidValue" },
        { "split", """{"mode":"exact","memberIds":["0199f1c2-0000-7000-8000-000000000001"]}""", "validation.invalidValue" }
    };

    public static TheoryData<string, string> ValidFields => new()
    {
        { "amount", "100000000000" },
        { "amount", "5.0" },
        { "currency", "\"KWD\"" },
        { "date", "\"2027-10-18\"" },
        { "categoryId", "\"other\"" },
        { "categoryId", "\"cafes\"" },
        { "categoryId", "\"0199f1c2-0000-7000-8000-000000000009\"" },
        { "categoryId", "null" },
        { "categoryId", "\"  \"" },
        { "tags", Tags(10) },
        { "tags", "[]" },
        { "tags", "null" },
        { "description", "\"" + new string('a', 100) + "\"" },
        { "description", "\"  " + new string('a', 100) + "  \"" },
        { "description", "\"   \"" },
        { "description", "null" },
        { "rate", "1e-9" },
        { "rate", "1000000000" },
        { "rate", "null" },
        { "split", "null" },
        { "split", Equal(Second) },
        { "split", Equal([.. Enumerable.Range(1, 50).Select(index => $"0199f1c2-0000-7000-8000-{index:D12}")]) },
        { "split", Exact((First, "4000"), (Second, "6000")) },
        { "split", Exact((Second, "10000")) }
    };

    [Fact]
    public void Should_Pass_When_AllFieldsValid() => Errors(Valid).ShouldBeEmpty();

    [Fact]
    public void Should_Pass_When_OptionalFieldsAbsent() =>
        Errors(Without(Valid, "categoryId", "tags", "description", "rate", "split")).ShouldBeEmpty();

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Should_ReportOneCode_When_FieldInvalid(string field, string json, string code) =>
        Errors(With(Valid, field, json)).ShouldBe([(Pascal(field), code)]);

    [Theory]
    [MemberData(nameof(ValidFields))]
    public void Should_Accept_When_FieldValid(string field, string json) =>
        Errors(With(Valid, field, json)).ShouldBeEmpty();

    [Theory]
    [InlineData("payerId")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("date")]
    public void Should_ReportRequired_When_FieldAbsent(string field) =>
        Errors(Without(Valid, field)).ShouldBe([(Pascal(field), "validation.required")]);

    [Fact]
    public void Should_AcceptExactSplit_When_SharesAddUpToTheAmount() =>
        Errors(With(With(Valid, "amount", "9999"), "split", Exact((First, "4000"), (Second, "5999")))).ShouldBeEmpty();

    [Fact]
    public void Should_SkipSplitSum_When_AmountInvalid() =>
        Errors(With(With(Valid, "amount", "0"), "split", Exact((First, "4000"), (Second, "5999"))))
            .ShouldBe([("Amount", "validation.positiveAmount")]);

    [Fact]
    public void Should_ReportEveryInvalidField_When_SeveralInvalid() =>
        Errors(With(With(With(Valid, "payerId", "\"x\""), "currency", "\"eur\""), "split", Equal("x"))).ShouldBe(
            [("PayerId", "validation.unknownMember"), ("Currency", "validation.invalidValue"), ("Split", "validation.unknownMember")],
            ignoreOrder: true);

    private static string Tags(int count) =>
        JsonSerializer.Serialize(Enumerable.Range(1, count).Select(index => $"Tag {index}"));

    private static string Equal(params string[] memberIds) =>
        JsonSerializer.Serialize(new { mode = "equal", memberIds });

    private static string Exact(params (string MemberId, string Amount)[] shares) =>
        $$"""{"mode":"exact","shares":[{{string.Join(',', shares.Select(share => $$"""{"memberId":"{{share.MemberId}}","amount":{{share.Amount}}}"""))}}]}""";

    private static string Pascal(string field) => char.ToUpperInvariant(field[0]) + field[1..];

    private static string With(string source, string field, string json)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(source)!;
        body[field] = JsonDocument.Parse(json).RootElement.Clone();
        return JsonSerializer.Serialize(body);
    }

    private static string Without(string source, params string[] fields)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(source)!;
        foreach (var field in fields)
        {
            body.Remove(field);
        }

        return JsonSerializer.Serialize(body);
    }

    private static (string, string)[] Errors(string json)
    {
        var request = JsonSerializer.Deserialize<CreateExpenseRequest>(json, JsonSerializerOptions.Web)!;
        var command = new CreateExpenseCommand(
            GroupScope.User(Guid.NewGuid()),
            request.PayerId,
            request.Amount,
            request.Currency,
            request.Date,
            request.CategoryId,
            request.Tags,
            request.Description,
            request.Rate,
            request.Split);
        var validator = new CreateExpenseValidator(new FixedTimeProvider(Now));
        return [.. validator.Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
