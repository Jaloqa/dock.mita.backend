using System.Text.Json;
using Mita.Api.Common.Groups;
using Mita.Api.Features.Groups.CreateGroupRepayment;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Groups;

public sealed class CreateGroupRepaymentValidatorTests
{
    private const string First = "0199f1c2-0000-7000-8000-000000000001";

    private const string Valid =
        """
        {"fromMemberId":"0199f1c2-0000-7000-8000-000000000001","toMemberId":"0199f1c2-0000-7000-8000-000000000002",
         "amount":10000,"currency":"EUR","date":"2026-10-18","rate":4.5,"note":"Cash"}
        """;

    private static readonly DateTimeOffset Now = new(2026, 10, 18, 23, 30, 0, TimeSpan.Zero);

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "fromMemberId", "null", "validation.required" },
        { "fromMemberId", "\"  \"", "validation.required" },
        { "fromMemberId", "\"x\"", "validation.unknownMember" },
        { "fromMemberId", "5", "validation.unknownMember" },
        { "toMemberId", "null", "validation.required" },
        { "toMemberId", "\"x\"", "validation.unknownMember" },
        { "toMemberId", "5", "validation.unknownMember" },
        { "toMemberId", $"\"{First}\"", "validation.sameMember" },
        { "toMemberId", $"\"{First.ToUpperInvariant()}\"", "validation.sameMember" },
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
        { "rate", "0", "validation.invalidValue" },
        { "rate", "\"4\"", "validation.invalidValue" },
        { "rate", "1000000001", "validation.invalidValue" },
        { "note", "5", "validation.invalidValue" },
        { "note", "\"A\\u0007\"", "validation.invalidValue" },
        { "note", "\"" + new string('a', 201) + "\"", "validation.noteTooLong" }
    };

    public static TheoryData<string, string> ValidFields => new()
    {
        { "fromMemberId", "\"0199f1c2-0000-7000-8000-000000000009\"" },
        { "toMemberId", "\"0199f1c2-0000-7000-8000-000000000009\"" },
        { "amount", "100000000000" },
        { "amount", "5.0" },
        { "currency", "\"KWD\"" },
        { "date", "\"2027-10-18\"" },
        { "rate", "1e-9" },
        { "rate", "1000000000" },
        { "rate", "null" },
        { "note", "\"" + new string('a', 200) + "\"" },
        { "note", "\"  " + new string('a', 200) + "  \"" },
        { "note", "\"   \"" },
        { "note", "null" }
    };

    [Fact]
    public void Should_Pass_When_AllFieldsValid() => Errors(Valid).ShouldBeEmpty();

    [Fact]
    public void Should_Pass_When_OptionalFieldsAbsent() => Errors(Without(Valid, "rate", "note")).ShouldBeEmpty();

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Should_ReportOneCode_When_FieldInvalid(string field, string json, string code) =>
        Errors(With(Valid, field, json)).ShouldBe([(Pascal(field), code)]);

    [Theory]
    [MemberData(nameof(ValidFields))]
    public void Should_Accept_When_FieldValid(string field, string json) =>
        Errors(With(Valid, field, json)).ShouldBeEmpty();

    [Theory]
    [InlineData("fromMemberId")]
    [InlineData("toMemberId")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("date")]
    public void Should_ReportRequired_When_FieldAbsent(string field) =>
        Errors(Without(Valid, field)).ShouldBe([(Pascal(field), "validation.required")]);

    [Fact]
    public void Should_SkipSameMember_When_FromMemberInvalid() =>
        Errors(With(With(Valid, "fromMemberId", "\"x\""), "toMemberId", "\"x\""))
            .ShouldBe([("FromMemberId", "validation.unknownMember"), ("ToMemberId", "validation.unknownMember")], ignoreOrder: true);

    [Fact]
    public void Should_ReportEveryInvalidField_When_SeveralInvalid() =>
        Errors(With(With(With(Valid, "toMemberId", $"\"{First}\""), "currency", "\"eur\""), "note", "5")).ShouldBe(
            [("ToMemberId", "validation.sameMember"), ("Currency", "validation.invalidValue"), ("Note", "validation.invalidValue")],
            ignoreOrder: true);

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
        var request = JsonSerializer.Deserialize<CreateGroupRepaymentRequest>(json, JsonSerializerOptions.Web)!;
        var command = new CreateGroupRepaymentCommand(
            GroupScope.Owner(Guid.NewGuid()),
            request.FromMemberId,
            request.ToMemberId,
            request.Amount,
            request.Currency,
            request.Date,
            request.Rate,
            request.Note);
        var validator = new CreateGroupRepaymentValidator(new FixedTimeProvider(Now));
        return [.. validator.Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
