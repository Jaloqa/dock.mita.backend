using Mita.Api.Features.Transactions.ListTransactions;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Transactions;

public sealed class ListTransactionsValidatorTests
{
    private static readonly ListTransactionsQuery Valid = new(null, null, null, null, null, null, null, null, 1, 20);

    public static TheoryData<string, string> InvalidQueries => new()
    {
        { "kind=x", "Kind" },
        { "from=2026-02-30", "From" },
        { "to=bad", "To" },
        { "sort=price", "Sort" },
        { "categoryId=abc", "CategoryId" },
        { "accountId=abc", "AccountId" },
        { "tagIds=abc", "TagIds" },
        { $"tagIds={Guid.NewGuid()},abc", "TagIds" },
        { $"tagIds={string.Join(',', Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()))}", "TagIds" },
        { "pageSize=101", "PageSize" },
        { "page=0", "Page" }
    };

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public void Should_ReportInvalidValue_When_QueryValueIsInvalid(string parameter, string property) =>
        Errors(With(parameter)).ShouldBe([(property, "validation.invalidValue")]);

    [Fact]
    public void Should_BeValid_When_AllValuesAreNull() => Errors(Valid).ShouldBeEmpty();

    [Fact]
    public void Should_BeValid_When_ValuesAreWellFormed() =>
        Errors(Valid with
        {
            Search = "%",
            CategoryId = Guid.NewGuid().ToString(),
            AccountId = Guid.NewGuid().ToString(),
            TagIds = $"{Guid.NewGuid()}, {Guid.NewGuid()}",
            Kind = "income",
            From = "2026-02-28",
            To = "2026-03-01",
            Sort = "-amount",
            Page = 3,
            PageSize = 100
        }).ShouldBeEmpty();

    [Fact]
    public void Should_BeValid_When_OneTagId() => Errors(Valid with { TagIds = Guid.NewGuid().ToString() }).ShouldBeEmpty();

    [Fact]
    public void Should_BeValid_When_TagIdIsDuplicated()
    {
        var id = Guid.NewGuid();

        Errors(Valid with { TagIds = $"{id},{id}" }).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("-date")]
    [InlineData("date")]
    [InlineData("-amount")]
    [InlineData("amount")]
    public void Should_AcceptSort_When_Known(string sort) => Errors(Valid with { Sort = sort }).ShouldBeEmpty();

    private static ListTransactionsQuery With(string parameter)
    {
        var (name, value) = (parameter.Split('=')[0], parameter.Split('=')[1]);
        return name switch
        {
            "kind" => Valid with { Kind = value },
            "from" => Valid with { From = value },
            "to" => Valid with { To = value },
            "sort" => Valid with { Sort = value },
            "categoryId" => Valid with { CategoryId = value },
            "accountId" => Valid with { AccountId = value },
            "tagIds" => Valid with { TagIds = value },
            "pageSize" => Valid with { PageSize = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) },
            "page" => Valid with { Page = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) },
            _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, null)
        };
    }

    private static (string, string)[] Errors(ListTransactionsQuery query) =>
        [.. new ListTransactionsValidator().Validate(query).Errors.Select(error => (error.PropertyName, error.ErrorCode))];
}
