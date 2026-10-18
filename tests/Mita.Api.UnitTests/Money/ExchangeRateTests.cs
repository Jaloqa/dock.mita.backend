using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.UnitTests.Money;

public sealed class ExchangeRateTests
{
    [Fact]
    public void Should_CrossThroughPln_When_ComputingRate()
    {
        ExchangeRate.Cross(4.27m, 3.93m).ShouldBe(1.086514m);
        ExchangeRate.Round(0.0000004m).ShouldBe(0m);
    }

    [Fact]
    public void Should_QuoteEveryCurrencyExceptPln()
    {
        ExchangeRate.Quoted.ShouldBe([Currency.EUR, Currency.USD, Currency.UAH]);
    }
}
