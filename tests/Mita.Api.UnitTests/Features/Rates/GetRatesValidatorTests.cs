using Mita.Api.Features.Rates.GetRates;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Rates;

public sealed class GetRatesValidatorTests
{
    public static TheoryData<string, string?, string?, string> InvalidQueries => new()
    {
        { "2026-02-30", "EUR", null, "Date" },
        { "2026-1-01", "EUR", null, "Date" },
        { "x", "EUR", null, "Date" },
        { "2026-03-02", "pln", "EUR", "From" },
        { "2026-03-02", "XDR", "EUR", "From" },
        { "2026-03-02", "EUR", "pln", "To" },
        { "2026-03-02", null, "XDR", "To" }
    };

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public void Should_ReportInvalidValue_When_QueryValueIsInvalid(string date, string? from, string? to, string property) =>
        Errors(date, from, to).ShouldBe([(property, "validation.invalidValue")]);

    [Fact]
    public void Should_ReportRequired_When_FromAndToAreNull() =>
        Errors("2026-03-02", null, null).ShouldBe([("To", "validation.required")]);

    [Theory]
    [InlineData(null, "JPY")]
    [InlineData("JPY", null)]
    public void Should_BeValid_When_OnlyOneSideIsGiven(string? from, string? to) => Errors("2026-03-02", from, to).ShouldBeEmpty();

    [Fact]
    public void Should_ReportDateFirst_When_DateInvalidAndSidesMissing() =>
        Errors("x", null, null).ShouldBe([("Date", "validation.invalidValue"), ("To", "validation.required")]);

    [Fact]
    public void Should_BeValid_When_ValuesAreWellFormed() => Errors("2026-02-28", "EUR", "UAH").ShouldBeEmpty();

    private static (string, string)[] Errors(string date, string? from, string? to) =>
        [.. new GetRatesValidator().Validate(new GetRatesQuery(date, from, to, "2026-03-02")).Errors
            .Select(error => (error.PropertyName, error.ErrorCode))];
}
