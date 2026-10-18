using System.Text.Json;
using Mita.Api.Common.Json;
using Mita.Domain.Users;
using Shouldly;

namespace Mita.Api.UnitTests.Common;

public sealed class MonthStartJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new MonthStartJsonConverter() } };

    [Theory]
    [InlineData("1", 1)]
    [InlineData("15", 15)]
    [InlineData("28", 28)]
    public void Should_ReadDay_When_NumberInRange(string json, int expected) =>
        JsonSerializer.Deserialize<MonthStart>(json, Options).Value.ShouldBe(expected);

    [Fact]
    public void Should_ReadLastWorkingDay_When_StringMatches() =>
        JsonSerializer.Deserialize<MonthStart>("\"lastWorkingDay\"", Options).IsLastWorkingDay.ShouldBeTrue();

    [Theory]
    [InlineData("\"x\"")]
    [InlineData("\"LASTWORKINGDAY\"")]
    [InlineData("0")]
    [InlineData("29")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("{}")]
    public void Should_Throw_When_ValueInvalid(string json) =>
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<MonthStart>(json, Options));

    [Fact]
    public void Should_WriteNumber_When_DaySet() =>
        JsonSerializer.Serialize(MonthStart.Day(1), Options).ShouldBe("1");

    [Fact]
    public void Should_WriteLastWorkingDayString_When_NoDay() =>
        JsonSerializer.Serialize(MonthStart.LastWorkingDay, Options).ShouldBe("\"lastWorkingDay\"");
}