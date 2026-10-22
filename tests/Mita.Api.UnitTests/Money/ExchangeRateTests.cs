using System.Globalization;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Money;

public sealed class ExchangeRateTests
{
    [Fact]
    public void Should_CrossThroughPln_When_ComputingRate()
    {
        ExchangeRate.Cross(4.27m, 3.93m).ShouldBe(1.086514m);
        ExchangeRate.Round(0.0000004m).ShouldBe(0.0000004m);
    }

    [Theory]
    [InlineData("4.256349", "4.256349")]
    [InlineData("1.0913589744", "1.091359")]
    [InlineData("0.1", "0.1")]
    [InlineData("0.0247644", "0.0247644")]
    [InlineData("0.02476449", "0.0247645")]
    [InlineData("0.00000212", "0.00000212")]
    [InlineData("0.00000016795", "0.00000016795")]
    [InlineData("0.0000000000004", "0")]
    [InlineData("-0.02476449", "-0.0247645")]
    [InlineData("-0.0247645", "-0.0247645")]
    public void Should_KeepSixSignificantDigits_When_RateIsSmall(string rate, string expected) =>
        ExchangeRate.Round(decimal.Parse(rate, CultureInfo.InvariantCulture))
            .ShouldBe(decimal.Parse(expected, CultureInfo.InvariantCulture));
}
