using Mita.Api.Common.Transactions;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Transactions;

public sealed class TransactionPriceTests
{
    private static readonly Dictionary<Currency, decimal> Rates = new()
    {
        [Currency.PLN] = 1m,
        [Currency.EUR] = 4.2717m,
        [Currency.USD] = 3.93m
    };

    [Fact]
    public void Should_KeepAmount_When_AllCurrenciesEqual() =>
        TransactionPrice.Compute(1_000, Currency.PLN, Currency.PLN, Currency.PLN, null, Rates)
            .ShouldBe(new TransactionPrice(1m, 1_000, 1_000));

    [Fact]
    public void Should_UseStoredRate_When_CurrencyDiffersFromAccount() =>
        TransactionPrice.Compute(10_000, Currency.EUR, Currency.PLN, Currency.PLN, null, Rates)
            .ShouldBe(new TransactionPrice(4.2717m, 42_717, 42_717));

    [Fact]
    public void Should_UseEnteredAmount_When_CurrencyIsBase() =>
        TransactionPrice.Compute(10_000, Currency.EUR, Currency.PLN, Currency.EUR, null, Rates)
            .ShouldBe(new TransactionPrice(4.2717m, 42_717, 10_000));

    [Fact]
    public void Should_ConvertAccountAmount_When_BaseDiffers() =>
        TransactionPrice.Compute(10_000, Currency.EUR, Currency.PLN, Currency.USD, null, Rates)
            .ShouldBe(new TransactionPrice(4.2717m, 42_717, 10_869));

    [Fact]
    public void Should_UseManualRate_When_Given() =>
        TransactionPrice.Compute(10_000, Currency.EUR, Currency.PLN, Currency.PLN, 4.5m, Rates)
            .ShouldBe(new TransactionPrice(4.5m, 45_000, 45_000));

    [Fact]
    public void Should_RoundManualRate_When_MoreThanSixDecimals() =>
        TransactionPrice.Compute(1_000_000, Currency.EUR, Currency.PLN, Currency.PLN, 4.12345650m, Rates)
            .ShouldBe(new TransactionPrice(4.123457m, 4_123_457, 4_123_457));

    [Fact]
    public void Should_IgnoreManualRate_When_CurrencyEqualsAccount() =>
        TransactionPrice.Compute(1_000, Currency.PLN, Currency.PLN, Currency.PLN, 4.5m, Rates)
            .ShouldBe(new TransactionPrice(1m, 1_000, 1_000));

    [Fact]
    public void Should_ScaleToAccountDigits_When_JpyEnteredOnPlnAccount() =>
        TransactionPrice.Compute(
                1_500,
                Currency.JPY,
                Currency.PLN,
                Currency.PLN,
                null,
                new Dictionary<Currency, decimal> { [Currency.PLN] = 1m, [Currency.JPY] = 0.024764m })
            .ShouldBe(new TransactionPrice(0.024764m, 3_715, 3_715));

    [Fact]
    public void Should_ScaleToAccountDigits_When_KwdEnteredOnEurAccount()
    {
        var price = TransactionPrice.Compute(
            1_250,
            Currency.KWD,
            Currency.EUR,
            Currency.PLN,
            null,
            new Dictionary<Currency, decimal> { [Currency.PLN] = 1m, [Currency.KWD] = 12.6221m, [Currency.EUR] = 4.3685m });

        price.ShouldNotBeNull();
        price.Rate.ShouldBe(2.889344m);
        price.AccountAmount.ShouldBe(361);
    }

    [Fact]
    public void Should_ReturnNull_When_EnteredCurrencyHasNoRate() =>
        TransactionPrice.Compute(10_000, Currency.EUR, Currency.PLN, Currency.PLN, null, new Dictionary<Currency, decimal> { [Currency.PLN] = 1m })
            .ShouldBeNull();

    [Fact]
    public void Should_ReturnNull_When_BaseCurrencyHasNoRate() =>
        TransactionPrice.Compute(1_000, Currency.PLN, Currency.PLN, Currency.USD, null, new Dictionary<Currency, decimal> { [Currency.PLN] = 1m })
            .ShouldBeNull();
}
