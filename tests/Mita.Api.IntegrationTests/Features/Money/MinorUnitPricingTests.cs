using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.Common.Transactions;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Money;

public sealed class MinorUnitPricingTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string Date = "2026-03-02";
    private const decimal JpyPln = 0.024764m;
    private const decimal KwdPln = 12.6221m;
    private const decimal EurPln = 4.2563m;
    private const decimal IrrPln = 0.00000212m;

    private static readonly decimal[] RoundRows =
    [
        4.256349m, 1.0913589744m, 0.1m, 0.0247644m, 0.02476449m, 0.00000212m, 0.00000016795m, 0.0000000000004m,
        -0.0247645m, -0.02476449m
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_StoreTinyPlnValue_Exactly()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.IRR, Date, IrrPln));

        await using var scope = isolated.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.ExchangeRates.AsNoTracking()
            .Where(rate => rate.Currency == Currency.IRR)
            .Select(rate => rate.PlnValue)
            .SingleAsync(Ct);

        stored.ShouldBe(IrrPln);
    }

    [Fact]
    public async Task Should_PriceJpyExpense_On_PlnAccount()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.JPY, Date, JpyPln));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN", openingBalance: 10_000);
        var category = await CategoryIdAsync(client);

        var created = await CreateTransactionAsync(client, category, account, 1_500, "JPY");

        created.GetProperty("accountAmount").GetInt64().ShouldBe(3_715);
        created.GetProperty("baseAmount").GetInt64().ShouldBe(3_715);
        (await BalanceAsync(client, account)).ShouldBe(10_000 - 3_715);
    }

    [Fact]
    public async Task Should_ConvertBetweenExponents_When_CurrenciesDiffer()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.JPY, Date, JpyPln), (Currency.KWD, Date, KwdPln));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var kwd = await CreateAccountAsync(client, "KWD");
        var pln = await CreateAccountAsync(client, "PLN");
        var jpy = await CreateAccountAsync(client, "JPY");

        using var toPln = await PostTransferAsync(client, kwd, pln, 1_250);
        using var toKwd = await PostTransferAsync(client, jpy, kwd, 1_500);

        toPln.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await toPln.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("toAmount").GetInt64().ShouldBe(1_578);
        toKwd.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await toKwd.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("rate").GetDecimal().ShouldBe(0.00196196m);
        body.GetProperty("toAmount").GetInt64().ShouldBe(2_943);
    }

    [Fact]
    public async Task Should_MatchCSharpPricing_When_BaseCurrencyChangesWithJpyAccount()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.JPY, Date, JpyPln), (Currency.EUR, Date, EurPln));
        var plnValues = new Dictionary<Currency, decimal>
        {
            [Currency.PLN] = 1m,
            [Currency.JPY] = JpyPln,
            [Currency.EUR] = EurPln
        };
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "JPY");
        var category = await CategoryIdAsync(client);
        var entered = new[] { (Amount: 123_457L, Currency: Currency.JPY), (Amount: 9_999L, Currency: Currency.PLN), (Amount: 1_234L, Currency: Currency.EUR) };
        var ids = new List<(Guid Id, long Amount, Currency Currency)>();
        foreach (var (amount, currency) in entered)
        {
            var created = await CreateTransactionAsync(client, category, account, amount, currency.ToString());
            ids.Add((created.GetProperty("id").GetGuid(), amount, currency));
        }

        (await PutBaseCurrencyAsync(client, "EUR")).ShouldBe(HttpStatusCode.OK);

        using var response = await client.Http.GetAsync("/v1/transactions?pageSize=100", Ct);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetGuid(), item => item.GetProperty("baseAmount").GetInt64());
        foreach (var (id, amount, currency) in ids)
        {
            var expected = TransactionPrice.Compute(amount, currency, Currency.JPY, Currency.EUR, null, plnValues);
            expected.ShouldNotBeNull();
            items[id].ShouldBe(expected.BaseAmount);
        }
    }

    [Fact]
    public async Task Should_RoundLikeCSharp_InSql()
    {
        var faker = new Faker { Random = new Randomizer(2026) };
        var rates = Enumerable.Range(0, 50)
            .Select(_ => faker.Random.Decimal(1m, 10m) * Pow10(faker.Random.Int(-9, 6)))
            .Concat(RoundRows)
            .ToList();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var rate in rates)
        {
            var rounded = await db.Database.SqlQuery<decimal>($"""SELECT round_rate({rate}) AS "Value" """).SingleAsync(Ct);
            rounded.ShouldBe(ExchangeRate.Round(rate), $"round_rate({rate})");
        }

        foreach (var currency in Currencies.All)
        {
            var code = currency.ToString();
            var exponent = await db.Database.SqlQuery<int>($"""SELECT currency_exponent({code}) AS "Value" """).SingleAsync(Ct);
            exponent.ShouldBe(MinorUnits.Exponent(currency), code);
        }
    }

    [Fact]
    public async Task Should_AcceptManualRateUpToOneBillion()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var kwd = await CreateAccountAsync(client, "KWD");
        var irr = await CreateAccountAsync(client, "IRR");

        using var max = await PostTransferAsync(client, kwd, irr, 1, rate: 1_000_000_000m);
        using var above = await PostTransferAsync(client, kwd, irr, 1, rate: 1_000_000_001m);
        using var overflow = await PostTransferAsync(client, kwd, irr, MinorUnits.MaxAmount, rate: 1_000_000_000m);

        max.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await max.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("toAmount").GetInt64().ShouldBe(100_000_000);
        above.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await above.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(["rate"]);
        errors.GetProperty("rate").EnumerateArray().Select(code => code.GetString()).ShouldBe(["validation.invalidValue"]);
        overflow.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await overflow.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors").GetProperty("rate")
            .EnumerateArray().Select(code => code.GetString()).ShouldBe(["validation.invalidValue"]);
    }

    private static decimal Pow10(int exponent)
    {
        var value = 1m;
        for (var i = 0; i < Math.Abs(exponent); i++)
        {
            value = exponent > 0 ? value * 10m : value / 10m;
        }

        return value;
    }

    private static async Task<Guid> CreateAccountAsync(AuthClient client, string currency, long openingBalance = 0)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = "Account",
            ["type"] = "cash",
            ["currency"] = currency,
            ["openingBalance"] = openingBalance,
            ["archived"] = false
        };
        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, "/v1/accounts") { Content = JsonContent.Create(body) }),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<long> BalanceAsync(AuthClient client, Guid accountId)
    {
        using var response = await client.Http.GetAsync("/v1/accounts", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == accountId)
            .GetProperty("balance").GetInt64();
    }

    private static async Task<Guid> CategoryIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/categories", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "Groceries" && item.GetProperty("kind").GetString() == "expense")
            .GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> CreateTransactionAsync(
        AuthClient client,
        Guid category,
        Guid account,
        long amount,
        string currency)
    {
        var body = new Dictionary<string, object?>
        {
            ["kind"] = "expense",
            ["date"] = Date,
            ["categoryId"] = category,
            ["accountId"] = account,
            ["amount"] = amount,
            ["currency"] = currency
        };
        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, "/v1/transactions") { Content = JsonContent.Create(body) }),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<HttpResponseMessage> PostTransferAsync(
        AuthClient client,
        Guid from,
        Guid to,
        long amount,
        decimal? rate = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["date"] = Date,
            ["fromAccountId"] = from,
            ["toAccountId"] = to,
            ["fromAmount"] = amount
        };
        if (rate is not null)
        {
            body["rate"] = rate;
        }

        return client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, "/v1/transfers") { Content = JsonContent.Create(body) }),
            Ct);
    }

    private static async Task<HttpStatusCode> PutBaseCurrencyAsync(AuthClient client, string baseCurrency)
    {
        var body = new Dictionary<string, object?>
        {
            ["baseCurrency"] = baseCurrency,
            ["locale"] = "en",
            ["monthStartDay"] = 1,
            ["theme"] = "system",
            ["notifications"] = new { budgetLimit = false, dailyReminder = true, weeklyReport = true, goals = false }
        };
        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, "/v1/settings") { Content = JsonContent.Create(body) }),
            Ct);
        return response.StatusCode;
    }
}
