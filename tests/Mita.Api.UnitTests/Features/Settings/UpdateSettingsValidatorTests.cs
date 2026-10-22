using System.Text.Json;
using Mita.Api.Features.Settings.UpdateSettings;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Settings;

public sealed class UpdateSettingsValidatorTests
{
    private const string Valid =
        """{"baseCurrency":"PLN","locale":"en","monthStartDay":1,"theme":"system","notifications":{"budgetLimit":true,"dailyReminder":false,"weeklyReport":false,"goals":true}}""";

    public static TheoryData<string, string, string> InvalidFields => new()
    {
        { "baseCurrency", "\"GBP\"", "validation.invalidValue" },
        { "baseCurrency", "\"JPY\"", "validation.invalidValue" },
        { "baseCurrency", "\"pln\"", "validation.invalidValue" },
        { "baseCurrency", "\"\"", "validation.invalidValue" },
        { "baseCurrency", "5", "validation.invalidValue" },
        { "baseCurrency", "true", "validation.invalidValue" },
        { "baseCurrency", "null", "validation.required" },
        { "locale", "\"de\"", "validation.invalidValue" },
        { "locale", "\"EN\"", "validation.invalidValue" },
        { "locale", "1", "validation.invalidValue" },
        { "locale", "null", "validation.required" },
        { "theme", "\"auto\"", "validation.invalidValue" },
        { "theme", "\"Dark\"", "validation.invalidValue" },
        { "theme", "[]", "validation.invalidValue" },
        { "theme", "null", "validation.required" },
        { "monthStartDay", "0", "validation.invalidValue" },
        { "monthStartDay", "29", "validation.invalidValue" },
        { "monthStartDay", "31", "validation.invalidValue" },
        { "monthStartDay", "-1", "validation.invalidValue" },
        { "monthStartDay", "1.5", "validation.invalidValue" },
        { "monthStartDay", "\"x\"", "validation.invalidValue" },
        { "monthStartDay", "\"1\"", "validation.invalidValue" },
        { "monthStartDay", "null", "validation.required" },
        { "notifications", "\"yes\"", "validation.invalidValue" },
        { "notifications", "[]", "validation.invalidValue" },
        { "notifications", "{}", "validation.invalidValue" },
        { "notifications", """{"budgetLimit":true,"dailyReminder":true,"weeklyReport":true}""", "validation.invalidValue" },
        { "notifications", """{"budgetLimit":"true","dailyReminder":true,"weeklyReport":true,"goals":true}""", "validation.invalidValue" },
        { "notifications", """{"budgetLimit":null,"dailyReminder":true,"weeklyReport":true,"goals":true}""", "validation.invalidValue" },
        { "notifications", "null", "validation.required" }
    };

    public static TheoryData<string, string> ValidFields => new()
    {
        { "baseCurrency", "\"EUR\"" },
        { "baseCurrency", "\"USD\"" },
        { "baseCurrency", "\"UAH\"" },
        { "locale", "\"pl\"" },
        { "locale", "\"ru\"" },
        { "locale", "\"uk\"" },
        { "theme", "\"light\"" },
        { "theme", "\"dark\"" },
        { "monthStartDay", "28" },
        { "monthStartDay", "\"lastWorkingDay\"" },
        { "notifications", """{"budgetLimit":false,"dailyReminder":false,"weeklyReport":true,"goals":false,"extra":1}""" }
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
    [InlineData("baseCurrency")]
    [InlineData("locale")]
    [InlineData("monthStartDay")]
    [InlineData("theme")]
    [InlineData("notifications")]
    public void Should_ReportRequired_When_FieldAbsent(string field) =>
        Errors(Without(field)).ShouldBe([(Pascal(field), "validation.required")]);

    [Fact]
    public void Should_ReportEveryField_When_BodyEmpty() =>
        Errors("{}").Select(error => error.Item1).ShouldBe(
            ["BaseCurrency", "Locale", "MonthStartDay", "Theme", "Notifications"],
            ignoreOrder: true);

    private static string Pascal(string field) => char.ToUpperInvariant(field[0]) + field[1..];

    private static string With(string field, string json)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Valid)!;
        body[field] = JsonDocument.Parse(json).RootElement.Clone();
        return JsonSerializer.Serialize(body);
    }

    private static string Without(string field)
    {
        var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Valid)!;
        body.Remove(field);
        return JsonSerializer.Serialize(body);
    }

    private static (string, string)[] Errors(string json)
    {
        var request = JsonSerializer.Deserialize<UpdateSettingsRequest>(json, JsonSerializerOptions.Web)!;
        var command = new UpdateSettingsCommand(
            request.BaseCurrency, request.Locale, request.MonthStartDay, request.Theme, request.Notifications);
        return [.. new UpdateSettingsValidator().Validate(command).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
    }
}