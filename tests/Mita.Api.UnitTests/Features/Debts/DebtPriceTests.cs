using Mita.Api.Common.Debts;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Debts;

public sealed class DebtPriceTests
{
    private static readonly Dictionary<Currency, decimal> Rates = new()
    {
        [Currency.PLN] = 1m,
        [Currency.EUR] = 4.2563m
    };

    [Fact]
    public void Should_UseStoredRate_When_CurrencyDiffersFromAccount() =>
        DebtPrice.Compute(5_000, Currency.EUR, Currency.PLN, null, Rates)
            .ShouldBe(new DebtPrice(4.2563m, 21_282));

    [Fact]
    public void Should_UseManualRate_When_Given() =>
        DebtPrice.Compute(5_000, Currency.EUR, Currency.PLN, 4.5m, Rates)
            .ShouldBe(new DebtPrice(4.5m, 22_500));

    [Fact]
    public void Should_RoundManualRate_When_MoreThanSixDecimals() =>
        DebtPrice.Compute(1_000_000, Currency.EUR, Currency.PLN, 4.12345650m, Rates)
            .ShouldBe(new DebtPrice(4.123457m, 4_123_457));

    [Fact]
    public void Should_ScaleToAccountDigits_When_JpyDebtOnPlnAccount() =>
        DebtPrice.Compute(
                1_500,
                Currency.JPY,
                Currency.PLN,
                null,
                new Dictionary<Currency, decimal> { [Currency.PLN] = 1m, [Currency.JPY] = 0.024764m })
            .ShouldBe(new DebtPrice(0.024764m, 3_715));

    [Fact]
    public void Should_KeepAmount_When_CurrencyEqualsAccountAndRateGiven() =>
        DebtPrice.Compute(700, Currency.PLN, Currency.PLN, 9m, Rates)
            .ShouldBe(new DebtPrice(1m, 700));

    [Fact]
    public void Should_ReturnNoPrice_When_NoAccount() =>
        DebtPrice.Compute(700, Currency.PLN, null, 9m, Rates)
            .ShouldBe(new DebtPrice(null, null));

    [Fact]
    public void Should_ReturnNull_When_DebtCurrencyHasNoRate() =>
        DebtPrice.Compute(5_000, Currency.EUR, Currency.PLN, null, new Dictionary<Currency, decimal> { [Currency.PLN] = 1m })
            .ShouldBeNull();
}
