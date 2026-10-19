using Mita.Api.Features.Overview.GetOverview;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Overview;

public sealed class GetOverviewValidatorTests
{
    [Theory]
    [InlineData("day")]
    [InlineData("2026-10")]
    [InlineData("Week")]
    [InlineData("")]
    public void Should_ReportInvalidValue_When_PeriodIsUnknown(string period) =>
        Errors(new GetOverviewQuery(period)).ShouldBe([("Period", "validation.invalidValue")]);

    [Theory]
    [InlineData(null)]
    [InlineData("week")]
    [InlineData("month")]
    [InlineData("year")]
    public void Should_BeValid_When_PeriodIsAbsentOrKnown(string? period) =>
        Errors(new GetOverviewQuery(period)).ShouldBeEmpty();

    private static (string, string)[] Errors(GetOverviewQuery query) =>
        [.. new GetOverviewValidator().Validate(query).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
}
