using System.Text.Json;
using Mita.Api.Features.ToConfirm.ConfirmItem;
using Mita.Api.Features.ToConfirm.RejectItem;
using Shouldly;

namespace Mita.Api.UnitTests.Features.ToConfirm;

public sealed class ConfirmItemValidatorTests
{
    private const string Version = "0199f1c2-0000-7000-8000-000000000001.0199f1c2-0000-7000-8000-000000000004";

    private const string Valid =
        """
        {"version":"0199f1c2-0000-7000-8000-000000000001.0199f1c2-0000-7000-8000-000000000004",
         "accountId":"0199f1c2-0000-7000-8000-000000000002","rate":4.3,
         "lines":[{"categoryId":"0199f1c2-0000-7000-8000-000000000003","title":"Dinner","tags":["dinner"]},
                  {"newCategory":"Trips","title":null,"tags":null}]}
        """;

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "version", "null", "validation.required" },
        { "version", "\"  \"", "validation.required" },
        { "version", "5", "validation.invalidValue" },
        { "version", "true", "validation.invalidValue" },
        { "version", "[\"x\"]", "validation.invalidValue" },
        { "accountId", "\"x\"", "validation.unknownAccount" },
        { "accountId", "5", "validation.unknownAccount" },
        { "rate", "0", "validation.invalidValue" },
        { "rate", "-1", "validation.invalidValue" },
        { "rate", "\"4\"", "validation.invalidValue" },
        { "rate", "1000000001", "validation.invalidValue" },
        { "lines", "\"x\"", "validation.invalidValue" },
        { "lines", "{}", "validation.invalidValue" },
        { "lines", "5", "validation.invalidValue" },
        { "lines", "[\"x\"]", "validation.invalidValue" },
        { "lines", "[null]", "validation.invalidValue" }
    };

    public static TheoryData<string, string, string> InvalidLineFields => new()
    {
        { "{\"categoryId\":\"0199f1c2-0000-7000-8000-000000000003\",\"newCategory\":\"Trips\"}", "categoryId", "validation.invalidValue" },
        { "{\"categoryId\":\"x\",\"newCategory\":\"Trips\"}", "categoryId", "validation.invalidValue" },
        { "{}", "categoryId", "validation.required" },
        { "{\"categoryId\":null,\"newCategory\":\"  \"}", "categoryId", "validation.required" },
        { "{\"categoryId\":\"x\"}", "categoryId", "validation.unknownCategory" },
        { "{\"categoryId\":5}", "categoryId", "validation.unknownCategory" },
        { "{\"newCategory\":\"" + new string('a', 41) + "\"}", "newCategory", "validation.nameTooLong" },
        { "{\"newCategory\":5}", "newCategory", "validation.invalidValue" },
        { "{\"newCategory\":\"Trips\\u0007\"}", "newCategory", "validation.invalidValue" },
        { "{\"newCategory\":\"Trips\",\"title\":\"" + new string('a', 101) + "\"}", "title", "validation.titleTooLong" },
        { "{\"newCategory\":\"Trips\",\"title\":5}", "title", "validation.invalidValue" },
        { "{\"newCategory\":\"Trips\",\"title\":\"A\\u0007\"}", "title", "validation.invalidValue" },
        { "{\"newCategory\":\"Trips\",\"tags\":" + Tags(11) + "}", "tags", "validation.tooManyTags" },
        { "{\"newCategory\":\"Trips\",\"tags\":\"x\"}", "tags", "validation.invalidValue" },
        { "{\"newCategory\":\"Trips\",\"tags\":[\"" + new string('a', 31) + "\"]}", "tags", "validation.tagTooLong" }
    };

    public static TheoryData<string> ValidLines => new()
    {
        "{\"categoryId\":\"0199f1c2-0000-7000-8000-000000000003\"}",
        "{\"categoryId\":\"0199f1c2-0000-7000-8000-000000000003\",\"newCategory\":null}",
        "{\"categoryId\":null,\"newCategory\":\"Trips\"}",
        "{\"newCategory\":\"  " + new string('a', 40) + "  \"}",
        "{\"newCategory\":\"Trips\",\"title\":\"" + new string('a', 100) + "\",\"tags\":" + Tags(10) + "}",
        "{\"newCategory\":\"Trips\",\"title\":\"   \",\"tags\":[]}"
    };

    [Fact]
    public void Should_Pass_When_AllFieldsValid() => Errors(Valid).ShouldBeEmpty();

    [Fact]
    public void Should_Pass_When_OnlyVersionIsSent() =>
        Errors(Without(Valid, "accountId", "rate", "lines")).ShouldBeEmpty();

    [Fact]
    public void Should_Pass_When_LinesAreEmpty() => Errors(With(Valid, "lines", "[]")).ShouldBeEmpty();

    [Fact]
    public void Should_ReportRequired_When_VersionAbsent() =>
        Errors(Without(Valid, "version")).ShouldBe([("version", "validation.required")]);

    [Theory]
    [InlineData("\"x\"")]
    [InlineData("\"0199f1c2-0000-7000-8000-000000000001\"")]
    public void Should_Pass_When_VersionIsAnyText(string json) => Errors(With(Valid, "version", json)).ShouldBeEmpty();

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void Should_ReportOneCode_When_FieldInvalid(string field, string json, string code) =>
        Errors(With(Valid, field, json)).ShouldBe([(field, code)]);

    [Theory]
    [MemberData(nameof(InvalidLineFields))]
    public void Should_ReportLineField_When_LineFieldInvalid(string line, string field, string code) =>
        Errors(With(Valid, "lines", $"[{line}]")).ShouldBe([($"lines.0.{field}", code)]);

    [Theory]
    [MemberData(nameof(ValidLines))]
    public void Should_Accept_When_LineValid(string line) => Errors(With(Valid, "lines", $"[{line}]")).ShouldBeEmpty();

    [Fact]
    public void Should_ReportEveryInvalidLineField_When_SeveralInvalid() =>
        Errors(With(
                With(Valid, "version", "5"),
                "lines",
                "[{\"newCategory\":\"Trips\"},{\"title\":5,\"tags\":" + Tags(11) + "}]"))
            .ShouldBe(
                [
                    ("version", "validation.invalidValue"),
                    ("lines.1.categoryId", "validation.required"),
                    ("lines.1.title", "validation.invalidValue"),
                    ("lines.1.tags", "validation.tooManyTags")
                ],
                ignoreOrder: true);

    [Theory]
    [InlineData("{}", "validation.required")]
    [InlineData("{\"version\":null}", "validation.required")]
    [InlineData("{\"version\":5}", "validation.invalidValue")]
    public void Should_ReportVersion_When_RejectVersionInvalid(string json, string code) =>
        RejectErrors(json).ShouldBe([("version", code)]);

    [Theory]
    [InlineData(Version)]
    [InlineData("x")]
    public void Should_PassReject_When_VersionIsText(string version) =>
        RejectErrors($"{{\"version\":\"{version}\"}}").ShouldBeEmpty();

    private static string Tags(int count) =>
        JsonSerializer.Serialize(Enumerable.Range(1, count).Select(index => $"tag{index}").ToArray());

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
        var request = JsonSerializer.Deserialize<ConfirmItemRequest>(json, JsonSerializerOptions.Web)!;
        var command = new ConfirmItemCommand(
            "expenses",
            Guid.NewGuid(),
            request.Version,
            request.AccountId,
            request.Rate,
            request.Lines);
        return [.. new ConfirmItemValidator().Validate(command).Errors.Select(error => (Camel(error.PropertyName), error.ErrorCode))];
    }

    private static (string, string)[] RejectErrors(string json)
    {
        var request = JsonSerializer.Deserialize<RejectItemRequest>(json, JsonSerializerOptions.Web)!;
        var command = new RejectItemCommand("expenses", Guid.NewGuid(), request.Version);
        return [.. new RejectItemValidator().Validate(command).Errors.Select(error => (Camel(error.PropertyName), error.ErrorCode))];
    }

    private static string Camel(string path) =>
        string.Join('.', path.Split('.').Select(segment =>
            segment.Length == 0 ? segment : char.ToLowerInvariant(segment[0]) + segment[1..]));
}
