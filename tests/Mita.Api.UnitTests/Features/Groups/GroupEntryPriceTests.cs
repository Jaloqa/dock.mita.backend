using Mita.Api.Common.Groups;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Groups;

public sealed class GroupEntryPriceTests
{
    private static readonly Dictionary<Currency, decimal> Rates = new()
    {
        [Currency.PLN] = 1m,
        [Currency.ALL] = 0.0440m,
        [Currency.EUR] = 4.30m,
        [Currency.JPY] = 0.024764m
    };

    [Fact]
    public void Should_KeepAmount_When_EntryInGroupCurrency() =>
        GroupEntryPrice.Compute(5_000, Currency.EUR, Currency.EUR, null, Rates)
            .ShouldBe(new GroupEntryPrice(1m, false, 5_000));

    [Fact]
    public void Should_IgnoreManualRate_When_EntryInGroupCurrency() =>
        GroupEntryPrice.Compute(5_000, Currency.EUR, Currency.EUR, 4.5m, Rates)
            .ShouldBe(new GroupEntryPrice(1m, false, 5_000));

    [Fact]
    public void Should_UseStoredCrossRate_When_CurrencyDiffers() =>
        GroupEntryPrice.Compute(300_000, Currency.ALL, Currency.EUR, null, Rates)
            .ShouldBe(new GroupEntryPrice(0.0102326m, false, 3_070));

    [Fact]
    public void Should_UseManualRate_When_Given() =>
        GroupEntryPrice.Compute(10_000, Currency.PLN, Currency.EUR, 0.25m, Rates)
            .ShouldBe(new GroupEntryPrice(0.25m, true, 2_500));

    [Fact]
    public void Should_ScaleToGroupDigits_When_EntryInJpy() =>
        GroupEntryPrice.Compute(1_500, Currency.JPY, Currency.EUR, null, Rates)
            .ShouldBe(new GroupEntryPrice(0.00575907m, false, 864));

    [Fact]
    public void Should_ReturnNull_When_EntryCurrencyHasNoRate() =>
        GroupEntryPrice.Compute(
                300_000,
                Currency.ALL,
                Currency.EUR,
                null,
                new Dictionary<Currency, decimal> { [Currency.PLN] = 1m, [Currency.EUR] = 4.30m })
            .ShouldBeNull();
}
