using Mita.Api.Features.Tags.ListTags;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Tags;

public sealed class ListTagsValidatorTests
{
    [Theory]
    [InlineData(1, 100)]
    [InlineData(3, 1)]
    public void Should_Pass_When_PagingValid(int page, int pageSize) =>
        new ListTagsValidator().Validate(new ListTagsQuery(null, page, pageSize)).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData(1, 101, "PageSize")]
    [InlineData(1, 0, "PageSize")]
    [InlineData(0, 100, "Page")]
    public void Should_ReportInvalidValue_When_PagingOutOfRange(int page, int pageSize, string property) =>
        new ListTagsValidator().Validate(new ListTagsQuery(null, page, pageSize)).Errors
            .Select(error => (error.PropertyName, error.ErrorCode))
            .ShouldBe([(property, "validation.invalidValue")]);
}
