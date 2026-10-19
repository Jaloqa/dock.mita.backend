using Mita.Api.Features.Budgets.ListBudgets;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Budgets;

public sealed class ListBudgetsValidatorTests
{
    private static readonly ListBudgetsQuery Valid = new(null, 1, 100);

    [Theory]
    [InlineData("2026-13")]
    [InlineData("26-10")]
    [InlineData("abc")]
    [InlineData("2026-10-01")]
    [InlineData("0000-01")]
    public void Should_ReportInvalidValue_When_PeriodIsNotAKey(string period) =>
        Errors(Valid with { Period = period }).ShouldBe([("Period", "validation.invalidValue")]);

    [Fact]
    public void Should_ReportInvalidValue_When_PageSizeIsTooLarge() =>
        Errors(Valid with { PageSize = 101 }).ShouldBe([("PageSize", "validation.invalidValue")]);

    [Fact]
    public void Should_ReportInvalidValue_When_PageIsZero() =>
        Errors(Valid with { Page = 0 }).ShouldBe([("Page", "validation.invalidValue")]);

    [Fact]
    public void Should_BeValid_When_AllValuesAreNull() => Errors(Valid).ShouldBeEmpty();

    [Fact]
    public void Should_BeValid_When_PeriodIsAKey() => Errors(Valid with { Period = "2026-10" }).ShouldBeEmpty();

    private static (string, string)[] Errors(ListBudgetsQuery query) =>
        [.. new ListBudgetsValidator().Validate(query).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
}
