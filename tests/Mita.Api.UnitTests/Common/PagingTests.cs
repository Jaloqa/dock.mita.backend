using System.Text.Json;
using Mita.Api.Common;
using Shouldly;

namespace Mita.Api.UnitTests.Common;

public sealed class PagingTests
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    private readonly PageRequestValidator validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_RejectPage_When_ZeroOrNegative(int page)
    {
        var result = validator.Validate(new PageRequest(Page: page));

        var error = result.Errors.ShouldHaveSingleItem();
        error.PropertyName.ShouldBe(nameof(PageRequest.Page));
        error.ErrorCode.ShouldBe("validation.invalidValue");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void Should_RejectPageSize_When_OutsideAllowedRange(int pageSize)
    {
        var result = validator.Validate(new PageRequest(PageSize: pageSize));

        var error = result.Errors.ShouldHaveSingleItem();
        error.PropertyName.ShouldBe(nameof(PageRequest.PageSize));
        error.ErrorCode.ShouldBe("validation.invalidValue");
    }

    [Fact]
    public void Should_AcceptDefaults_When_NothingSpecified() =>
        validator.Validate(new PageRequest()).IsValid.ShouldBeTrue();

    [Fact]
    public void Should_SerialiseWithFrontendFieldNames()
    {
        var json = JsonSerializer.Serialize(
            new PageResponse<string>([], 1, 20, 0),
            WebOptions);

        json.ShouldBe("{\"items\":[],\"page\":1,\"pageSize\":20,\"total\":0}");
    }

    [Fact]
    public void Should_SkipPreviousPages() =>
        new PageRequest(Page: 3, PageSize: 20).Skip.ShouldBe(40);

    [Fact]
    public void Should_AcceptPage_When_OffsetFitsInInt()
    {
        var request = new PageRequest(Page: 21474837, PageSize: 100);

        validator.Validate(request).IsValid.ShouldBeTrue();
        request.Skip.ShouldBe(2147483600);
    }

    [Theory]
    [InlineData(21474838, 100)]
    [InlineData(int.MaxValue, 100)]
    public void Should_RejectPage_When_OffsetExceedsInt(int page, int pageSize)
    {
        var request = new PageRequest(page, pageSize);

        var error = validator.Validate(request).Errors.ShouldHaveSingleItem();
        error.PropertyName.ShouldBe(nameof(PageRequest.Page));
        error.ErrorCode.ShouldBe("validation.invalidValue");
        request.Skip.ShouldBe(int.MaxValue);
    }
}
