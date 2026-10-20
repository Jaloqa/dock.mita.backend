using Mita.Api.Features.Rates.GetRates;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Rates;

public sealed class GetRatesValidatorTests
{
    public static TheoryData<string, string?, string?, string> InvalidQueries => new()
    {
        { "2026-02-30", null, null, "Date" },
        { "2026-1-01", null, null, "Date" },
        { "x", null, null, "Date" },
        { "2026-03-02", "pln", null, "From" },
        { "2026-03-02", "GBP", null, "From" },
        { "2026-03-02", null, "pln", "To" },
        { "2026-03-02", null, "GBP", "To" }
    };

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public void Should_ReportInvalidValue_When_QueryValueIsInvalid(string date, string? from, string? to, string property) =>
        Errors(date, from, to).ShouldBe([(property, "validation.invalidValue")]);

    [Fact]
    public void Should_BeValid_When_FromAndToAreNull() => Errors("2026-03-02", null, null).ShouldBeEmpty();

    [Fact]
    public void Should_BeValid_When_ValuesAreWellFormed() => Errors("2026-02-28", "EUR", "UAH").ShouldBeEmpty();

    private static (string, string)[] Errors(string date, string? from, string? to) =>
        [.. new GetRatesValidator().Validate(new GetRatesQuery(date, from, to, "2026-03-02")).Errors
            .Select(error => (error.PropertyName, error.ErrorCode))];
}
