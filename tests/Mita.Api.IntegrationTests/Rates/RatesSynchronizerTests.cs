using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Mita.Infrastructure.Rates;
using Shouldly;

namespace Mita.Api.IntegrationTests.Rates;

public sealed class RatesSynchronizerTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_BackfillHistory_When_TableIsEmpty()
    {
        var source = new FakeExchangeRateSource();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        await using var host = Host(source, clock);

        var inserted = await SyncAsync(host);

        source.Requests.ShouldBe([(new DateOnly(2026, 4, 18), new DateOnly(2026, 10, 18))]);
        inserted.ShouldBe(184 * 3);
        (await CountAsync(host)).ShouldBe(184 * 3);
        var stored = await RowAsync(host, Currency.EUR, new DateOnly(2026, 10, 18));
        stored.PlnValue.ShouldBe(FakeExchangeRateSource.DefaultValue(Currency.EUR, new DateOnly(2026, 10, 18)));
        stored.FetchedAt.ShouldBe(clock.GetUtcNow());
    }

    [Fact]
    public async Task Should_FetchOnlyNewDays_When_HistoryExists()
    {
        var source = new FakeExchangeRateSource();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        await using var host = Host(source, clock);
        await SyncAsync(host);

        clock.Advance(TimeSpan.FromDays(2));
        var inserted = await SyncAsync(host);

        source.Requests.Count.ShouldBe(2);
        source.Requests[1].ShouldBe((new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 20)));
        inserted.ShouldBe(2 * 3);
    }

    [Fact]
    public async Task Should_SkipSource_When_AlreadyUpToDate()
    {
        var source = new FakeExchangeRateSource();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        await using var host = Host(source, clock);
        await SyncAsync(host);

        var inserted = await SyncAsync(host);

        inserted.ShouldBe(0);
        source.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_IgnoreDuplicates_When_RowsAlreadyStored()
    {
        var source = new FakeExchangeRateSource();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        await using var host = Host(source, clock);
        await TestData.AddRatesAsync(
            host,
            (Currency.EUR, "2026-10-16", 9.9m),
            (Currency.EUR, "2026-10-17", 9.9m));

        var inserted = await SyncAsync(host);

        inserted.ShouldBe(184 * 3 - 2);
        (await CountAsync(host)).ShouldBe(184 * 3);
        (await RowAsync(host, Currency.EUR, new DateOnly(2026, 10, 16))).PlnValue.ShouldBe(9.9m);
    }

    [Fact]
    public async Task Should_SkipNonPositiveAndAbsurdValues_When_SourceReturnsThem()
    {
        var source = new FakeExchangeRateSource
        {
            ValueFor = (currency, _) => currency switch
            {
                Currency.EUR => 0m,
                Currency.USD => -1m,
                _ => 2_000_000m
            }
        };
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        await using var host = Host(source, clock);

        var inserted = await SyncAsync(host);

        inserted.ShouldBe(0);
        (await CountAsync(host)).ShouldBe(0);
    }

    [Fact]
    public async Task Should_InvalidateRatesCache_When_RowsInserted()
    {
        var source = new FakeExchangeRateSource { ValueFor = (_, _) => 4.5m };
        await using var host = factory.WithIsolatedData().WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IExchangeRateSource>(source)));
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var yesterdayText = yesterday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await TestData.AddRatesAsync(
            host,
            (Currency.EUR, yesterdayText, 4.0m),
            (Currency.USD, yesterdayText, 3.0m),
            (Currency.UAH, yesterdayText, 0.1m));
        using var client = new AuthClient(host);
        await client.RegisterNewAsync(Ct);

        var before = await GetEurToPlnAsync(client);
        var inserted = await SyncAsync(host);
        var after = await GetEurToPlnAsync(client);

        before.ShouldBe(4.0);
        inserted.ShouldBe(3);
        after.ShouldBe(4.5);
    }

    [Fact]
    public async Task Should_KeepRunning_When_SourceThrows()
    {
        var source = new FakeExchangeRateSource { Failure = new HttpRequestException("NBP is down") };
        var warning = new WarningCapture<RatesSyncService>();
        await using var host = factory.WithIsolatedData().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Rates:SyncEnabled"] = "true" }));
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IExchangeRateSource>(source);
                services.AddSingleton<ILogger<RatesSyncService>>(warning);
            });
        });
        await TestData.AddRatesAsync(host, (Currency.EUR, "2026-03-02", 4.27m));
        using var client = new AuthClient(host);
        await client.RegisterNewAsync(Ct);

        await warning.Logged.WaitAsync(TimeSpan.FromSeconds(30), Ct);

        source.Requests.ShouldNotBeEmpty();
        using var response = await client.Http.GetAsync("/v1/rates?date=2026-03-02&from=EUR&to=PLN", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<RateItem>>(Ct);
        items.ShouldNotBeNull().Single().Value.ShouldBe(4.27);
    }

    private WebApplicationFactory<Program> Host(FakeExchangeRateSource source, TimeProvider clock) =>
        factory.WithIsolatedData().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IExchangeRateSource>(source);
            services.AddSingleton(clock);
        }));

    private static async Task<int> SyncAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RatesSynchronizer>().SyncAsync(Ct);
    }

    private static async Task<int> CountAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ExchangeRates.CountAsync(Ct);
    }

    private static async Task<ExchangeRate> RowAsync(WebApplicationFactory<Program> host, Currency currency, DateOnly date)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ExchangeRates
            .AsNoTracking()
            .SingleAsync(rate => rate.Currency == currency && rate.Date == date, Ct);
    }

    private static async Task<double> GetEurToPlnAsync(AuthClient client)
    {
        var items = await client.Http.GetFromJsonAsync<List<RateItem>>("/v1/rates?from=EUR&to=PLN", Ct);
        return items.ShouldNotBeNull().Single().Value;
    }

    private sealed record RateItem(string Date, string From, string To, double Value);

    private sealed class WarningCapture<T> : ILogger<T>
    {
        private readonly TaskCompletionSource logged = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Logged => logged.Task;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning && exception is HttpRequestException)
            {
                logged.TrySetResult();
            }
        }
    }
}
