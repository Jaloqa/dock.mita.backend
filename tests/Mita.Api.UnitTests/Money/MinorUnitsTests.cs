using System.Globalization;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Money;

public sealed class MinorUnitsTests
{
    [Theory]
    [InlineData(1, "0.5", 1)]
    [InlineData(-1, "0.5", -1)]
    [InlineData(10000, "4.2717", 42717)]
    [InlineData(3, "0.333333", 1)]
    public void Should_RoundHalfAwayFromZero_When_Converting(long minor, string rate, long expected) =>
        MinorUnits.Convert(minor, decimal.Parse(rate, CultureInfo.InvariantCulture), Currency.PLN, Currency.PLN)
            .ShouldBe(expected);

    [Theory]
    [InlineData(Currency.JPY, 0)]
    [InlineData(Currency.KWD, 3)]
    [InlineData(Currency.PLN, 2)]
    public void Should_ReturnIsoExponent_When_CurrencyGiven(Currency currency, int expected) =>
        MinorUnits.Exponent(currency).ShouldBe(expected);

    [Fact]
    public void Should_UseTwoDecimals_When_CurrencyIsBase() =>
        Currencies.Base.ShouldAllBe(currency => MinorUnits.Exponent(currency) == 2);

    [Fact]
    public void Should_ListSixteenZeroAndSevenThreeDecimalCurrencies()
    {
        Currencies.All.Count(currency => MinorUnits.Exponent(currency) == 0).ShouldBe(16);
        Currencies.All.Count(currency => MinorUnits.Exponent(currency) == 3).ShouldBe(7);
        MinorUnits.ZeroDecimal.ShouldBe(
            [
                Currency.BIF, Currency.CLP, Currency.DJF, Currency.GNF, Currency.ISK, Currency.JPY, Currency.KMF,
                Currency.KRW, Currency.PYG, Currency.RWF, Currency.UGX, Currency.VND, Currency.VUV, Currency.XAF,
                Currency.XOF, Currency.XPF
            ],
            ignoreOrder: true);
        MinorUnits.ThreeDecimal.ShouldBe(
            [Currency.BHD, Currency.IQD, Currency.JOD, Currency.KWD, Currency.LYD, Currency.OMR, Currency.TND],
            ignoreOrder: true);
    }

    [Theory]
    [InlineData(1500, "0.024764", Currency.JPY, Currency.PLN, 3715)]
    [InlineData(1250, "12.6221", Currency.KWD, Currency.PLN, 1578)]
    [InlineData(3715, "40.38", Currency.PLN, Currency.JPY, 1500)]
    [InlineData(100, "0.0001503", Currency.VND, Currency.PLN, 2)]
    [InlineData(1000, "4.2563", Currency.EUR, Currency.PLN, 4256)]
    [InlineData(1, "0.5", Currency.PLN, Currency.PLN, 1)]
    public void Should_ScaleByExponents_When_Converting(long minor, string rate, Currency from, Currency to, long expected) =>
        MinorUnits.Convert(minor, decimal.Parse(rate, CultureInfo.InvariantCulture), from, to).ShouldBe(expected);

    [Fact]
    public void Should_Saturate_When_ConvertedAmountExceedsLongRange()
    {
        MinorUnits.Convert(MinorUnits.MaxAmount, 1_000_000_000m, Currency.JPY, Currency.KWD).ShouldBe(long.MaxValue);
        MinorUnits.Convert(-MinorUnits.MaxAmount, 1_000_000_000m, Currency.JPY, Currency.KWD).ShouldBe(long.MinValue);
    }
}
