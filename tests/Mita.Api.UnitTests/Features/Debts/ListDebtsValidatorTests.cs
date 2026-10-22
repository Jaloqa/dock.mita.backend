using Mita.Api.Features.Debts.ListDebts;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Debts;

public sealed class ListDebtsValidatorTests
{
    private static readonly ListDebtsQuery Valid = new(null, 1, 100);

    [Theory]
    [InlineData("open")]
    [InlineData("settled")]
    [InlineData(null)]
    public void Should_Accept_When_StatusKnown(string? status) => Errors(Valid with { Status = status }).ShouldBeEmpty();

    [Theory]
    [InlineData("Open")]
    [InlineData("SETTLED")]
    [InlineData("all")]
    [InlineData("")]
    public void Should_ReportInvalidValue_When_StatusUnknown(string status) =>
        Errors(Valid with { Status = status }).ShouldBe([("Status", "validation.invalidValue")]);

    [Fact]
    public void Should_ReportInvalidValue_When_PageSizeAboveMaximum() =>
        Errors(Valid with { PageSize = 101 }).ShouldBe([("PageSize", "validation.invalidValue")]);

    [Fact]
    public void Should_ReportInvalidValue_When_PageBelowOne() =>
        Errors(Valid with { Page = 0 }).ShouldBe([("Page", "validation.invalidValue")]);

    [Fact]
    public void Should_Accept_When_PagingAtLimits() =>
        Errors(Valid with { Page = 2, PageSize = 100 }).ShouldBeEmpty();

    private static (string, string)[] Errors(ListDebtsQuery query) =>
        [.. new ListDebtsValidator().Validate(query).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
}
