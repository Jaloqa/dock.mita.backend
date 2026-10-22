using Mita.Api.Features.Debts.AddRepayment;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Features.Debts;

public sealed class RepaymentPriceTests
{
    private static readonly Dictionary<Currency, decimal> Rates = new()
    {
        [Currency.PLN] = 1m,
        [Currency.EUR] = 4.2563m,
        [Currency.USD] = 3.9012m
    };

    [Fact]
    public void Should_ConvertToDebtCurrency_When_AccountInRepaymentCurrency() =>
        RepaymentPrice.Compute(5_000, Currency.EUR, Currency.PLN, Currency.EUR, null, Rates)
            .ShouldBe(new RepaymentPrice(4.2563m, 21_282, 5_000));

    [Fact]
    public void Should_UseDebtAmount_When_AccountInDebtCurrency() =>
        RepaymentPrice.Compute(5_000, Currency.EUR, Currency.PLN, Currency.PLN, null, Rates)
            .ShouldBe(new RepaymentPrice(4.2563m, 21_282, 21_282));

    [Fact]
    public void Should_UseCrossRateOfDate_When_AccountInThirdCurrency() =>
        RepaymentPrice.Compute(5_000, Currency.EUR, Currency.PLN, Currency.USD, null, Rates)
            .ShouldBe(new RepaymentPrice(
                4.2563m,
                21_282,
                MinorUnits.Convert(5_000, ExchangeRate.Cross(4.2563m, 3.9012m), Currency.EUR, Currency.USD)));

    [Fact]
    public void Should_RoundCrossConversion_When_EurIntoUsdAccount() =>
        RepaymentPrice.Compute(5_000, Currency.EUR, Currency.PLN, Currency.USD, null, Rates)!.AccountAmount.ShouldBe(5_455);

    [Fact]
    public void Should_IgnoreManualRateForAccount_When_AccountInThirdCurrency() =>
        RepaymentPrice.Compute(5_000, Currency.EUR, Currency.PLN, Currency.USD, 4.0m, Rates)
            .ShouldBe(new RepaymentPrice(4.0m, 20_000, 5_455));

    [Fact]
    public void Should_UseManualRateForDebt_When_AccountInRepaymentCurrency() =>
        RepaymentPrice.Compute(5_000, Currency.EUR, Currency.PLN, Currency.EUR, 4.0m, Rates)
            .ShouldBe(new RepaymentPrice(4.0m, 20_000, 5_000));

    [Fact]
    public void Should_RoundManualRate_When_MoreThanSixDecimals() =>
        RepaymentPrice.Compute(1_000_000, Currency.EUR, Currency.PLN, null, 4.12345650m, Rates)
            .ShouldBe(new RepaymentPrice(4.123457m, 4_123_457, null));

    [Fact]
    public void Should_ReturnNoAccountAmount_When_NoAccount() =>
        RepaymentPrice.Compute(5_000, Currency.EUR, Currency.PLN, null, null, Rates)
            .ShouldBe(new RepaymentPrice(4.2563m, 21_282, null));

    [Fact]
    public void Should_UseRateOne_When_CurrencyEqualsDebtCurrencyAndRateGiven() =>
        RepaymentPrice.Compute(1_000, Currency.PLN, Currency.PLN, Currency.PLN, 7m, Rates)
            .ShouldBe(new RepaymentPrice(1m, 1_000, 1_000));

    [Fact]
    public void Should_ScaleToDebtDigits_When_JpyRepaymentOnPlnDebt() =>
        RepaymentPrice.Compute(
                1_500,
                Currency.JPY,
                Currency.PLN,
                null,
                null,
                new Dictionary<Currency, decimal> { [Currency.PLN] = 1m, [Currency.JPY] = 0.024764m })
            .ShouldBe(new RepaymentPrice(0.024764m, 3_715, null));

    [Fact]
    public void Should_ReturnNull_When_RepaymentCurrencyHasNoRate() =>
        RepaymentPrice.Compute(
                5_000,
                Currency.EUR,
                Currency.PLN,
                Currency.EUR,
                null,
                new Dictionary<Currency, decimal> { [Currency.PLN] = 1m, [Currency.USD] = 3.9012m })
            .ShouldBeNull();

    [Fact]
    public void Should_ReturnNull_When_AccountCurrencyHasNoRate() =>
        RepaymentPrice.Compute(
                5_000,
                Currency.EUR,
                Currency.PLN,
                Currency.USD,
                null,
                new Dictionary<Currency, decimal> { [Currency.PLN] = 1m, [Currency.EUR] = 4.2563m })
            .ShouldBeNull();

    [Fact]
    public void Should_ReturnNull_When_ThirdCurrencyAccountHasNoRateEvenWithManualRate() =>
        RepaymentPrice.Compute(
                5_000,
                Currency.EUR,
                Currency.PLN,
                Currency.USD,
                4.0m,
                new Dictionary<Currency, decimal> { [Currency.PLN] = 1m })
            .ShouldBeNull();

    [Fact]
    public void Should_NeedNoStoredRate_When_ManualRateAndNoAccount() =>
        RepaymentPrice.Compute(5_000, Currency.EUR, Currency.PLN, null, 4.0m, new Dictionary<Currency, decimal> { [Currency.PLN] = 1m })
            .ShouldBe(new RepaymentPrice(4.0m, 20_000, null));
}
