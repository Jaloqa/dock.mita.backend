using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mita.Domain.Money;
using Mita.Infrastructure;
using Mita.Infrastructure.Rates;
using Shouldly;

namespace Mita.Api.UnitTests.Rates;

public sealed class NbpExchangeRateSourceTests
{
    private const string TwoDayTable = """
        [
          {
            "table": "A", "no": "001/A/NBP/2026", "effectiveDate": "2026-01-02",
            "rates": [
              { "currency": "euro", "code": "EUR", "mid": 4.2717 },
              { "currency": "dolar amerykanski", "code": "USD", "mid": 3.9312 },
              { "currency": "hrywna", "code": "UAH", "mid": 0.0948 },
              { "currency": "funt szterling", "code": "GBP", "mid": 5.0001 }
            ]
          },
          {
            "table": "A", "no": "002/A/NBP/2026", "effectiveDate": "2026-01-05",
            "rates": [
              { "currency": "euro", "code": "EUR", "mid": 4.2801 },
              { "currency": "dolar amerykanski", "code": "USD", "mid": 3.9400 },
              { "currency": "hrywna", "code": "UAH", "mid": 0.0951 },
              { "currency": "funt szterling", "code": "GBP", "mid": 5.0100 }
            ]
          }
        ]
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_ParseMidRates_When_NbpAnswersTable()
    {
        using var handler = new StubHandler(_ => Json(TwoDayTable));
        using var http = Client(handler);

        var rates = await new NbpExchangeRateSource(http, NullLogger<NbpExchangeRateSource>.Instance).FetchAsync(NbpTable.A, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6), Ct);

        rates.Count.ShouldBe(8);
        rates.ShouldContain(new PlnRate(Currency.EUR, new DateOnly(2026, 1, 2), 4.2717m));
        rates.ShouldContain(new PlnRate(Currency.GBP, new DateOnly(2026, 1, 5), 5.0100m));
        rates.ShouldContain(new PlnRate(Currency.USD, new DateOnly(2026, 1, 2), 3.9312m));
        rates.ShouldContain(new PlnRate(Currency.UAH, new DateOnly(2026, 1, 2), 0.0948m));
        rates.ShouldContain(new PlnRate(Currency.EUR, new DateOnly(2026, 1, 5), 4.2801m));
        rates.ShouldAllBe(rate => rate.Currency != Currency.PLN);
    }

    [Fact]
    public async Task Should_ParseTableB_When_NbpAnswers()
    {
        const string tableB = """
            [
              {
                "table": "B", "no": "042/B/NBP/2026", "effectiveDate": "2026-10-21",
                "rates": [
                  { "currency": "dinar kuwejcki", "code": "KWD", "mid": 12.6221 },
                  { "currency": "rial irański", "code": "IRR", "mid": 0.00000212 },
                  { "currency": "dong wietnamski", "code": "VND", "mid": 0.0001503 },
                  { "currency": "nieznana", "code": "ZZZ", "mid": 1.0 }
                ]
              }
            ]
            """;
        using var handler = new StubHandler(_ => Json(tableB));
        using var http = Client(handler);
        var logger = new CapturingLogger<NbpExchangeRateSource>();

        var rates = await new NbpExchangeRateSource(http, logger)
            .FetchAsync(NbpTable.B, new DateOnly(2026, 10, 21), new DateOnly(2026, 10, 21), Ct);

        rates.Count.ShouldBe(3);
        rates.ShouldContain(new PlnRate(Currency.KWD, new DateOnly(2026, 10, 21), 12.6221m));
        rates.ShouldContain(new PlnRate(Currency.IRR, new DateOnly(2026, 10, 21), 0.00000212m));
        rates.ShouldContain(new PlnRate(Currency.VND, new DateOnly(2026, 10, 21), 0.0001503m));
        handler.Requests.Single().RequestUri!.AbsolutePath.ShouldContain("/tables/B/");
        logger.Warnings.Count.ShouldBe(1);
        logger.Warnings.Single().ShouldContain("ZZZ");
    }

    [Fact]
    public async Task Should_IgnoreXdr_When_TableAContainsIt()
    {
        const string tableA = """
            [
              {
                "table": "A", "no": "001/A/NBP/2026", "effectiveDate": "2026-01-02",
                "rates": [
                  { "currency": "SDR (MFW)", "code": "XDR", "mid": 5.5 },
                  { "currency": "jen", "code": "JPY", "mid": 0.024764 }
                ]
              }
            ]
            """;
        using var handler = new StubHandler(_ => Json(tableA));
        using var http = Client(handler);
        var logger = new CapturingLogger<NbpExchangeRateSource>();

        var rates = await new NbpExchangeRateSource(http, logger)
            .FetchAsync(NbpTable.A, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 2), Ct);

        rates.ShouldBe([new PlnRate(Currency.JPY, new DateOnly(2026, 1, 2), 0.024764m)]);
        logger.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NbpAnswers404()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = Client(handler);

        var rates = await new NbpExchangeRateSource(http, NullLogger<NbpExchangeRateSource>.Instance).FetchAsync(NbpTable.A, new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 4), Ct);

        rates.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ThrowHttpRequestException_When_NbpAnswers500()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var http = Client(handler);

        await Should.ThrowAsync<HttpRequestException>(
            () => new NbpExchangeRateSource(http, NullLogger<NbpExchangeRateSource>.Instance).FetchAsync(NbpTable.A, new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 4), Ct));
    }

    [Fact]
    public async Task Should_SplitIntoWindows_When_RangeExceeds93Days()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = Client(handler);

        await new NbpExchangeRateSource(http, NullLogger<NbpExchangeRateSource>.Instance).FetchAsync(NbpTable.A, new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1), Ct);

        handler.Requests.Select(request => request.RequestUri!.AbsolutePath).ShouldBe(
        [
            "/api/exchangerates/tables/A/2026-01-01/2026-04-03/",
            "/api/exchangerates/tables/A/2026-04-04/2026-07-01/"
        ]);
        handler.Requests.ShouldAllBe(request => request.RequestUri!.Query == "?format=json");
    }

    [Fact]
    public async Task Should_MakeOneRequest_When_RangeIsExactly93Days()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = Client(handler);

        await new NbpExchangeRateSource(http, NullLogger<NbpExchangeRateSource>.Instance).FetchAsync(NbpTable.A, new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 3), Ct);

        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_RetryOnce_When_NbpAnswers503ThenOk()
    {
        var calls = 0;
        using var handler = new StubHandler(_ => Interlocked.Increment(ref calls) == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : Json(TwoDayTable));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(TimeProvider.System);
        services.AddRates();
        services.AddHttpClient<IExchangeRateSource, NbpExchangeRateSource>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.Configure<HttpStandardResilienceOptions>(
            "IExchangeRateSource-standard",
            options =>
            {
                options.Retry.Delay = TimeSpan.FromMilliseconds(1);
                options.Retry.UseJitter = false;
                options.Retry.BackoffType = Polly.DelayBackoffType.Constant;
            });
        await using var provider = services.BuildServiceProvider();

        var rates = await provider.GetRequiredService<IExchangeRateSource>()
            .FetchAsync(NbpTable.A, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6), Ct);

        handler.Requests.Count.ShouldBe(2);
        rates.Count.ShouldBe(8);
    }

    private static HttpClient Client(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.nbp.pl/api/") };

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly Lock gate = new();
        private readonly List<string> warnings = [];

        public IReadOnlyList<string> Warnings
        {
            get
            {
                lock (gate)
                {
                    return [.. warnings];
                }
            }
        }

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
            if (logLevel == LogLevel.Warning)
            {
                lock (gate)
                {
                    warnings.Add(formatter(state, exception));
                }
            }
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        private readonly Lock gate = new();
        private readonly List<HttpRequestMessage> requests = [];

        public IReadOnlyList<HttpRequestMessage> Requests
        {
            get
            {
                lock (gate)
                {
                    return [.. requests];
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                requests.Add(request);
            }

            return Task.FromResult(respond(request));
        }
    }
}
