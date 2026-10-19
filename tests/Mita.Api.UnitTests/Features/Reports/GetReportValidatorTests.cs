using Mita.Api.Features.Reports.GetReport;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Reports;

public sealed class GetReportValidatorTests
{
    [Theory]
    [InlineData("1m")]
    [InlineData("6M")]
    [InlineData("year")]
    [InlineData("6")]
    [InlineData("")]
    public void Should_ReportInvalidValue_When_RangeIsUnknown(string range) =>
        Errors(new GetReportQuery(range)).ShouldBe([("Range", "validation.invalidValue")]);

    [Theory]
    [InlineData(null)]
    [InlineData("3m")]
    [InlineData("6m")]
    [InlineData("12m")]
    public void Should_BeValid_When_RangeIsAbsentOrKnown(string? range) =>
        Errors(new GetReportQuery(range)).ShouldBeEmpty();

    private static (string, string)[] Errors(GetReportQuery query) =>
        [.. new GetReportValidator().Validate(query).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
}
