using System.Text.Json;
using Mita.Api.Features.Groups.CreateGroup;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Groups;

public sealed class CreateGroupValidatorTests
{
    private const string Valid = """{"name":"Albania 2026","currency":"EUR","members":["Anna","Olya","Max"]}""";

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "name", "null", "validation.required" },
        { "name", "\"   \"", "validation.required" },
        { "name", "5", "validation.invalidValue" },
        { "name", "\"Trip\\u0007\"", "validation.invalidValue" },
        { "name", "\"" + new string('a', 41) + "\"", "validation.nameTooLong" },
        { "currency", "null", "validation.required" },
        { "currency", "\"XDR\"", "validation.invalidValue" },
        { "currency", "\"eur\"", "validation.invalidValue" },
        { "currency", "5", "validation.invalidValue" },
        { "members", "null", "validation.required" },
        { "members", "[]", "validation.required" },
        { "members", "\"Anna\"", "validation.invalidValue" },
        { "members", "[\"Anna\", \" \"]", "validation.invalidValue" },
        { "members", "[\"Anna\", 5]", "validation.invalidValue" },
        { "members", "[\"Anna\", \"Ma\\u0007x\"]", "validation.invalidValue" },
        { "members", "[\"" + new string('a', 41) + "\"]", "validation.nameTooLong" },
        { "members", Names(51), "validation.tooManyMembers" },
        { "members", "[\"Max\", \" max \"]", "validation.memberNameTaken" },
        { "members", "[\"Olya\", \"OLYA\"]", "validation.memberNameTaken" }
    };

    public static TheoryData<string, string> ValidFields => new()
    {
        { "name", "\"" + new string('a', 40) + "\"" },
        { "name", "\"  " + new string('a', 40) + "  \"" },
        { "currency", "\"KWD\"" },
        { "members", "[\"Anna\"]" },
        { "members", "[\"" + new string('a', 40) + "\"]" },
        { "members", Names(50) },
        { "members", "[\"Anna\", \"Anya\"]" }
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
    [InlineData("currency")]
    [InlineData("members")]
    public void Should_ReportRequired_When_FieldAbsent(string field) =>
        Errors(Without(Valid, field)).ShouldBe([(Pascal(field), "validation.required")]);

    [Fact]
    public void Should_ReportTooManyMembers_When_NamesRepeatAboveTheLimit() =>
        Errors(With("members", RepeatedNames(51))).ShouldBe([("Members", "validation.tooManyMembers")]);

    [Fact]
    public void Should_ReportEveryInvalidField_When_SeveralInvalid() =>
        Errors("""{"name":" ","currency":"XDR","members":[]}""").ShouldBe(
            [("Name", "validation.required"), ("Currency", "validation.invalidValue"), ("Members", "validation.required")],
            ignoreOrder: true);

    private static string Names(int count) =>
        JsonSerializer.Serialize(Enumerable.Range(1, count).Select(index => $"Member {index}"));

    private static string RepeatedNames(int count) =>
        JsonSerializer.Serialize(Enumerable.Range(1, count).Select(_ => "Anna"));

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
        var request = JsonSerializer.Deserialize<CreateGroupRequest>(json, JsonSerializerOptions.Web)!;
        var command = new CreateGroupCommand(request.Name, request.Currency, request.Members);
        return [.. new CreateGroupValidator().Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }
}
