using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
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

        var rates = await new NbpExchangeRateSource(http).FetchAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6), Ct);

        rates.Count.ShouldBe(6);
        rates.ShouldContain(new PlnRate(Currency.EUR, new DateOnly(2026, 1, 2), 4.2717m));
        rates.ShouldContain(new PlnRate(Currency.USD, new DateOnly(2026, 1, 2), 3.9312m));
        rates.ShouldContain(new PlnRate(Currency.UAH, new DateOnly(2026, 1, 2), 0.0948m));
        rates.ShouldContain(new PlnRate(Currency.EUR, new DateOnly(2026, 1, 5), 4.2801m));
        rates.ShouldAllBe(rate => rate.Currency != Currency.PLN);
    }

    [Fact]
    public async Task Should_ReturnEmpty_When_NbpAnswers404()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = Client(handler);

        var rates = await new NbpExchangeRateSource(http).FetchAsync(new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 4), Ct);

        rates.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ThrowHttpRequestException_When_NbpAnswers500()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var http = Client(handler);

        await Should.ThrowAsync<HttpRequestException>(
            () => new NbpExchangeRateSource(http).FetchAsync(new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 4), Ct));
    }

    [Fact]
    public async Task Should_SplitIntoWindows_When_RangeExceeds93Days()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = Client(handler);

        await new NbpExchangeRateSource(http).FetchAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1), Ct);

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

        await new NbpExchangeRateSource(http).FetchAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 3), Ct);

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
            .FetchAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6), Ct);

        handler.Requests.Count.ShouldBe(2);
        rates.Count.ShouldBe(6);
    }

    private static HttpClient Client(HttpMessageHandler handler) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.nbp.pl/api/") };

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

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
