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
        MinorUnits.Convert(minor, decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture))
            .ShouldBe(expected);
}
