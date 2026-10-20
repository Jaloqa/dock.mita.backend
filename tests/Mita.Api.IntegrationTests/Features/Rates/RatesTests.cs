using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.Common.Wire;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Rates;

public sealed class RatesTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string RatesPath = "/v1/rates";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.GetAsync(RatesPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_ReturnTwelvePairs_When_NoFilter()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(
            isolated,
            (Currency.EUR, "2026-03-02", 4.27m),
            (Currency.USD, "2026-03-02", 3.93m),
            (Currency.UAH, "2026-03-02", 0.0912m));

        var items = await GetAsync(isolated, "date=2026-03-02");

        items.Count.ShouldBe(12);
        items.ShouldAllBe(item => item.From != item.To);
        items.Select(item => (item.From, item.To)).ShouldBe(
            [
                ("PLN", "EUR"), ("PLN", "USD"), ("PLN", "UAH"),
                ("EUR", "PLN"), ("EUR", "USD"), ("EUR", "UAH"),
                ("USD", "PLN"), ("USD", "EUR"), ("USD", "UAH"),
                ("UAH", "PLN"), ("UAH", "EUR"), ("UAH", "USD")
            ]);
        items.Single(item => item.From == "EUR" && item.To == "PLN").Value.ShouldBe(4.27);
        items.ShouldAllBe(item => item.Date == "2026-03-02");
    }

    [Fact]
    public async Task Should_ReturnCrossRate_When_NeitherSideIsPln()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(
            isolated,
            (Currency.EUR, "2026-03-02", 4.27m),
            (Currency.USD, "2026-03-02", 3.93m));

        var items = await GetAsync(isolated, "date=2026-03-02&from=EUR&to=USD");

        items.Count.ShouldBe(1);
        items[0].Value.ShouldBe(1.086514);
    }

    [Theory]
    [InlineData("2026-03-08")]
    [InlineData("2027-01-01")]
    public async Task Should_UseLatestEarlierRate_When_DateIsWeekendOrFuture(string date)
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(
            isolated,
            (Currency.EUR, "2026-03-05", 4.10m),
            (Currency.EUR, "2026-03-06", 4.27m));

        var items = await GetAsync(isolated, $"date={date}&from=EUR&to=PLN");

        items.Count.ShouldBe(1);
        items[0].Value.ShouldBe(4.27);
        items[0].Date.ShouldBe(date);
    }

    [Fact]
    public async Task Should_UseEarliestRate_When_DateBeforeHistory()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(
            isolated,
            (Currency.EUR, "2026-03-05", 4.10m),
            (Currency.EUR, "2026-03-06", 4.27m));

        var items = await GetAsync(isolated, "date=2020-01-01&from=EUR&to=PLN");

        items.Count.ShouldBe(1);
        items[0].Value.ShouldBe(4.10);
        items[0].Date.ShouldBe("2020-01-01");
    }

    [Fact]
    public async Task Should_OmitPairs_When_CurrencyHasNoRates()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, "2026-03-02", 4.27m));

        var items = await GetAsync(isolated, "date=2026-03-02&to=PLN");

        items.Select(item => (item.From, item.To)).ShouldBe([("EUR", "PLN")]);
    }

    [Fact]
    public async Task Should_DefaultToTodayUtc_When_DateOmitted()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, "2026-03-02", 4.27m));
        var before = IsoDate.ToWire(DateOnly.FromDateTime(DateTime.UtcNow));

        var items = await GetAsync(isolated);

        var after = IsoDate.ToWire(DateOnly.FromDateTime(DateTime.UtcNow));
        items.ShouldNotBeEmpty();
        items.ShouldAllBe(item => item.Date == before || item.Date == after);
    }

    [Fact]
    public async Task Should_ReadStoredRows_When_PastDateRequestedAgain()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, "2026-03-02", 4.10m));

        var first = await GetAsync(isolated, "date=2026-03-04&from=EUR&to=PLN");
        await TestData.AddRatesAsync(isolated, (Currency.EUR, "2026-03-03", 4.27m));
        var second = await GetAsync(isolated, "date=2026-03-04&from=EUR&to=PLN");

        first.Single().Value.ShouldBe(4.10);
        second.Single().Value.ShouldBe(4.27);
    }

    [Fact]
    public async Task Should_KeepTodaysRatesCached_When_RowsAddedWithoutSync()
    {
        await using var isolated = factory.WithIsolatedData();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDate.ToWire(today.AddDays(-1)), 4.0m));

        var first = await GetAsync(isolated, "from=EUR&to=PLN");
        await TestData.AddRatesAsync(isolated, (Currency.EUR, IsoDate.ToWire(today), 4.5m));
        var second = await GetAsync(isolated, "from=EUR&to=PLN");

        first.Single().Value.ShouldBe(4.0);
        second.Single().Value.ShouldBe(4.0);
    }

    [Fact]
    public async Task Should_Return400_When_QueryValueIsInvalid()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);

        using var response = await client.Http.GetAsync($"{RatesPath}?date=2026-02-30&from=eur", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("errors").GetProperty("date")[0].GetString().ShouldBe("validation.invalidValue");
        body.GetProperty("errors").GetProperty("from")[0].GetString().ShouldBe("validation.invalidValue");
    }

    private static async Task<List<RateItem>> GetAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, string? query = null)
    {
        using var client = new AuthClient(host);
        await client.RegisterNewAsync(Ct);

        using var response = await client.Http.GetAsync(query is null ? RatesPath : $"{RatesPath}?{query}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<List<RateItem>>(Ct) ?? [];
    }

    private sealed record RateItem(string Date, string From, string To, double Value);
}
