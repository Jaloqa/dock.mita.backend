using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Settings;

public sealed class BaseCurrencyChangeTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string TransactionDate = "2026-03-02";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_MatchFreshPricing_When_BaseCurrencyChanges()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, TransactionDate, 4.2717m), (Currency.USD, TransactionDate, 3.93m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var category = await CategoryIdAsync(client, "Groceries", "expense");
        var eur = await CreateTransactionAsync(client, category, account, 10_000, "EUR");
        var pln = await CreateTransactionAsync(client, category, account, 2_000, "PLN");

        (await PutSettingsAsync(client, "EUR")).ShouldBe(HttpStatusCode.OK);

        var page = await ListAsync(client);
        page.GetProperty("currency").GetString().ShouldBe("EUR");
        page.GetProperty("sum").GetInt64().ShouldBe(-10_468);
        var byId = page.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetGuid());
        byId[eur].GetProperty("baseAmount").GetInt64().ShouldBe(10_000);
        byId[pln].GetProperty("baseAmount").GetInt64().ShouldBe(468);

        (await PutSettingsAsync(client, "USD")).ShouldBe(HttpStatusCode.OK);

        var recomputed = (await ListAsync(client)).GetProperty("items").EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetGuid(), item => item.GetProperty("baseAmount").GetInt64());
        foreach (var (id, amount, currency) in new[] { (eur, 10_000L, "EUR"), (pln, 2_000L, "PLN") })
        {
            var fresh = await CreateTransactionAsync(client, category, account, amount, currency);
            var freshBase = (await GetAsync(client, fresh)).GetProperty("baseAmount").GetInt64();
            await DeleteAsync(client, fresh);
            recomputed[id].ShouldBe(freshBase);
        }
    }

    [Fact]
    public async Task Should_Return503AndKeepAmounts_When_RatesMissing()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var category = await CategoryIdAsync(client, "Groceries", "expense");
        var id = await CreateTransactionAsync(client, category, account, 10_000, "EUR", rate: 4.5);

        using var response = await SendSettingsAsync(client, "USD");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().ShouldBe("rates.unavailable");
        using var settings = await client.Http.GetAsync("/v1/settings", Ct);
        (await settings.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("baseCurrency").GetString().ShouldBe("PLN");
        (await GetAsync(client, id)).GetProperty("baseAmount").GetInt64().ShouldBe(45_000);
    }

    [Fact]
    public async Task Should_ChangeBaseCurrency_When_UserHasNoTransactions()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);

        (await PutSettingsAsync(client, "USD")).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_Return503_When_SameCurrencyTransactionsNeedBaseRate()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var category = await CategoryIdAsync(client, "Groceries", "expense");
        var id = await CreateTransactionAsync(client, category, account, 2_000, "PLN");

        using var response = await SendSettingsAsync(client, "EUR");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await GetAsync(client, id)).GetProperty("baseAmount").GetInt64().ShouldBe(2_000);
    }

    private static async Task<HttpStatusCode> PutSettingsAsync(AuthClient client, string baseCurrency)
    {
        using var response = await SendSettingsAsync(client, baseCurrency);
        return response.StatusCode;
    }

    private static Task<HttpResponseMessage> SendSettingsAsync(AuthClient client, string baseCurrency)
    {
        var body = new Dictionary<string, object?>
        {
            ["baseCurrency"] = baseCurrency,
            ["locale"] = "en",
            ["monthStartDay"] = 1,
            ["theme"] = "system",
            ["notifications"] = new { budgetLimit = false, dailyReminder = true, weeklyReport = true, goals = false }
        };
        return client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, "/v1/settings") { Content = JsonContent.Create(body) }),
            Ct);
    }

    private static async Task<Guid> CreateTransactionAsync(
        AuthClient client,
        Guid category,
        Guid account,
        long amount,
        string currency,
        double? rate = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["kind"] = "expense",
            ["date"] = TransactionDate,
            ["categoryId"] = category,
            ["accountId"] = account,
            ["amount"] = amount,
            ["currency"] = currency
        };
        if (rate is not null)
        {
            body["rate"] = rate;
        }

        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, "/v1/transactions") { Content = JsonContent.Create(body) }),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> GetAsync(AuthClient client, Guid id)
    {
        using var response = await client.Http.GetAsync($"/v1/transactions/{id}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task DeleteAsync(AuthClient client, Guid id)
    {
        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Delete, $"/v1/transactions/{id}")),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<JsonElement> ListAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/transactions?pageSize=100", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<Guid> CreateAccountAsync(AuthClient client, string currency)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = "Account",
            ["type"] = "cash",
            ["currency"] = currency,
            ["openingBalance"] = 0,
            ["archived"] = false
        };
        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, "/v1/accounts") { Content = JsonContent.Create(body) }),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CategoryIdAsync(AuthClient client, string name, string kind)
    {
        using var response = await client.Http.GetAsync("/v1/categories", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name && item.GetProperty("kind").GetString() == kind)
            .GetProperty("id").GetGuid();
    }
}
