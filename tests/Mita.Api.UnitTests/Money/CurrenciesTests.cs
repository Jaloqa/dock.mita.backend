using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Money;

public sealed class CurrenciesTests
{
    private static readonly string[] Expected = "PLN EUR USD UAH AED AFN ALL AMD AOA ARS AUD AWG AZN BAM BBD BDT BHD BIF BND BOB BRL BSD BWP BYN BZD CAD CDF CHF CLP CNY COP CRC CUP CVE CZK DJF DKK DOP DZD EGP ERN ETB FJD GBP GEL GHS GIP GMD GNF GTQ GYD HKD HNL HTG HUF IDR ILS INR IQD IRR ISK JMD JOD JPY KES KGS KHR KMF KRW KWD KZT LAK LBP LKR LRD LSL LYD MAD MDL MGA MKD MMK MNT MOP MRU MUR MVR MWK MXN MYR MZN NAD NGN NIO NOK NPR NZD OMR PAB PEN PGK PHP PKR PYG QAR RON RSD RUB RWF SAR SBD SCR SDG SEK SGD SLE SOS SRD SSP STN SVC SYP SZL THB TJS TMT TND TOP TRY TTD TWD TZS UGX UYU UZS VES VND VUV WST XAF XCD XCG XOF XPF YER ZAR ZMW ZWG ".Split(' ', StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Should_ListEveryNbpCurrency_When_ReadingAll()
    {
        Currencies.All.Count.ShouldBe(148);
        Currencies.All.Take(4).ShouldBe([Currency.PLN, Currency.EUR, Currency.USD, Currency.UAH]);
        var rest = Currencies.All.Skip(4).Select(currency => currency.ToString()).ToArray();
        rest.ShouldBe([.. rest.Order(StringComparer.Ordinal)]);
        Currencies.All.Select(currency => currency.ToString()).ShouldBe(Expected);
    }

    [Fact]
    public void Should_ExcludePln_When_ReadingQuoted()
    {
        Currencies.Quoted.Count.ShouldBe(147);
        Currencies.Quoted.ShouldNotContain(Currency.PLN);
    }

    [Fact]
    public void Should_KeepFourBaseCurrencies_When_ReadingBase()
    {
        Currencies.Base.ShouldBe([Currency.PLN, Currency.EUR, Currency.USD, Currency.UAH]);
        Currency.UAH.IsBase().ShouldBeTrue();
        Currency.JPY.IsBase().ShouldBeFalse();
    }

    [Theory]
    [InlineData("JPY", true)]
    [InlineData("ZWG", true)]
    [InlineData("XCG", true)]
    [InlineData("jpy", false)]
    [InlineData(" JPY", false)]
    [InlineData("JPY\n", false)]
    [InlineData("XDR", false)]
    [InlineData("1", false)]
    [InlineData("PLNX", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Should_ParseOnlyUpperCaseCodes_When_ParsingCurrency(string? code, bool expected)
    {
        Currencies.TryParse(code, out var currency).ShouldBe(expected);
        if (expected)
        {
            currency.ToString().ShouldBe(code);
        }
    }
}
