using Mita.Api.Configuration;
using Mita.Infrastructure.Rates;
using Shouldly;

namespace Mita.Api.UnitTests.Configuration;

public sealed class OptionsValidatorTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Should_RequirePositivePermitLimit_When_ValidatingRateLimitingOptions(int limit, bool valid) =>
        new RateLimitingOptionsValidator()
            .Validate(null, new RateLimitingOptions { AuthPermitLimit = limit })
            .Succeeded.ShouldBe(valid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Should_RequirePositiveSessionPermitLimit_When_ValidatingRateLimitingOptions(int limit, bool valid) =>
        new RateLimitingOptionsValidator()
            .Validate(null, new RateLimitingOptions { SessionPermitLimit = limit })
            .Succeeded.ShouldBe(valid);

    [Fact]
    public void Should_AcceptDefaults_When_ValidatingRatesOptions()
    {
        var options = new RatesOptions();

        new RatesOptionsValidator().Validate(null, options).Succeeded.ShouldBeTrue();
        options.SyncEnabled.ShouldBeTrue();
        options.SyncInterval.ShouldBe(TimeSpan.FromHours(6));
        options.HistoryDays.ShouldBe(183);
        options.NbpBaseUrl.ShouldBe(new Uri("https://api.nbp.pl/api/"));
    }

    [Theory]
    [InlineData(30, false)]
    [InlineData(60, true)]
    public void Should_RequireAtLeastOneMinute_When_ValidatingRatesSyncInterval(int seconds, bool valid) =>
        new RatesOptionsValidator()
            .Validate(null, new RatesOptions { SyncInterval = TimeSpan.FromSeconds(seconds) })
            .Succeeded.ShouldBe(valid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3650, true)]
    [InlineData(3651, false)]
    public void Should_RequireHistoryDaysInRange_When_ValidatingRatesOptions(int days, bool valid) =>
        new RatesOptionsValidator()
            .Validate(null, new RatesOptions { HistoryDays = days })
            .Succeeded.ShouldBe(valid);

    [Theory]
    [InlineData("https://api.nbp.pl/api/", true)]
    [InlineData("http://api.nbp.pl/api/", false)]
    public void Should_RequireHttpsUrl_When_ValidatingRatesNbpBaseUrl(string url, bool valid) =>
        new RatesOptionsValidator()
            .Validate(null, new RatesOptions { NbpBaseUrl = new Uri(url) })
            .Succeeded.ShouldBe(valid);

    [Fact]
    public void Should_RejectRelativeUrl_When_ValidatingRatesNbpBaseUrl() =>
        new RatesOptionsValidator()
            .Validate(null, new RatesOptions { NbpBaseUrl = new Uri("/api/", UriKind.Relative) })
            .Succeeded.ShouldBeFalse();

    [Fact]
    public void Should_AllowSixtySessionRequests_When_NotConfigured() =>
        new RateLimitingOptions().SessionPermitLimit.ShouldBe(60);

    [Theory]
    [InlineData("10.0.0.5", "10.0.0.0/8", true)]
    [InlineData("::1", "fd00::/8", true)]
    [InlineData("10.0.0.300", "10.0.0.0/8", false)]
    [InlineData("10.0.0.5", "10.0.0.0", false)]
    [InlineData("10.0.0.5", "not-a-network", false)]
    public void Should_RequireIpsAndCidrs_When_ValidatingTrustedProxiesOptions(string proxy, string network, bool valid) =>
        new TrustedProxiesOptionsValidator()
            .Validate(null, new TrustedProxiesOptions { KnownProxies = [proxy], KnownNetworks = [network] })
            .Succeeded.ShouldBe(valid);

    [Theory]
    [InlineData("http://localhost:3000", true)]
    [InlineData("https://app.example.com", true)]
    [InlineData("https://app.example.com/", false)]
    [InlineData("https://app.example.com/path", false)]
    [InlineData("ftp://example.com", false)]
    [InlineData("not a url", false)]
    public void Should_AcceptOnlyBareHttpOrigins_When_ValidatingCorsOptions(string origin, bool valid) =>
        new CorsOriginsOptionsValidator()
            .Validate(null, new CorsOriginsOptions { AllowedOrigins = [origin] })
            .Succeeded.ShouldBe(valid);
}
