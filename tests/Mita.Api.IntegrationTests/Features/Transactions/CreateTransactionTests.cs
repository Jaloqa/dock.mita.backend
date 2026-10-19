using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Transactions;

public sealed class CreateTransactionTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string TransactionsPath = "/v1/transactions";
    private const string AccountsPath = "/v1/accounts";
    private const string CategoriesPath = "/v1/categories";
    private const string TransactionDate = "2026-03-02";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.GetAsync($"{TransactionsPath}/{Guid.NewGuid()}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_CreateTransaction_When_SameCurrency()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN", openingBalance: 10_000);
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var salary = await CategoryIdAsync(client, "Salary", "income");

        using var response = await PostAsync(client, Body(groceries, account, 1_250, title: "  Bread ", note: "   "));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe($"/v1/transactions/{id}");
        body.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "kind", "date", "title", "note", "categoryId", "accountId", "amount", "currency", "accountAmount", "rate", "baseAmount"],
            ignoreOrder: true);
        body.GetProperty("kind").GetString().ShouldBe("expense");
        body.GetProperty("date").GetString().ShouldBe(TransactionDate);
        body.GetProperty("title").GetString().ShouldBe("Bread");
        body.GetProperty("note").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("categoryId").GetGuid().ShouldBe(groceries);
        body.GetProperty("accountId").GetGuid().ShouldBe(account);
        body.GetProperty("amount").GetInt64().ShouldBe(1_250);
        body.GetProperty("currency").GetString().ShouldBe("PLN");
        body.GetProperty("rate").GetDouble().ShouldBe(1);
        body.GetProperty("accountAmount").GetInt64().ShouldBe(1_250);
        body.GetProperty("baseAmount").GetInt64().ShouldBe(1_250);

        var accounts = await ListAsync(client, AccountsPath);
        accounts[account].GetProperty("balance").GetInt64().ShouldBe(8_750);
        accounts[account].GetProperty("inUse").GetBoolean().ShouldBeTrue();
        (await ListAsync(client, CategoriesPath))[groceries].GetProperty("inUse").GetBoolean().ShouldBeTrue();

        using var income = await PostAsync(client, Body(salary, account, 500, kind: "income"));

        income.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await income.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("title").GetString().ShouldBe(string.Empty);
        (await ListAsync(client, AccountsPath))[account].GetProperty("balance").GetInt64().ShouldBe(9_250);
    }

    [Fact]
    public async Task Should_ConvertIntoAccountCurrency_When_CurrencyDiffers()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, TransactionDate, 4.2717m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");

        using var stored = await PostAsync(client, Body(groceries, account, 10_000, currency: "EUR"));
        using var manual = await PostAsync(client, Body(groceries, account, 10_000, currency: "EUR", rate: 4.5));

        stored.StatusCode.ShouldBe(HttpStatusCode.Created);
        var storedBody = await stored.Content.ReadFromJsonAsync<JsonElement>(Ct);
        storedBody.GetProperty("amount").GetInt64().ShouldBe(10_000);
        storedBody.GetProperty("currency").GetString().ShouldBe("EUR");
        storedBody.GetProperty("rate").GetDouble().ShouldBe(4.2717);
        storedBody.GetProperty("accountAmount").GetInt64().ShouldBe(42_717);
        storedBody.GetProperty("baseAmount").GetInt64().ShouldBe(42_717);
        manual.StatusCode.ShouldBe(HttpStatusCode.Created);
        var manualBody = await manual.Content.ReadFromJsonAsync<JsonElement>(Ct);
        manualBody.GetProperty("rate").GetDouble().ShouldBe(4.5);
        manualBody.GetProperty("accountAmount").GetInt64().ShouldBe(45_000);
        manualBody.GetProperty("baseAmount").GetInt64().ShouldBe(45_000);
    }

    [Fact]
    public async Task Should_UseBaseRate_When_BaseCurrencyDiffers()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, TransactionDate, 4.2717m), (Currency.USD, TransactionDate, 3.93m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        using var settings = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, "/v1/settings") { Content = JsonContent.Create(SettingsBody("USD")) }),
            Ct);
        settings.StatusCode.ShouldBe(HttpStatusCode.OK);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");

        using var response = await PostAsync(client, Body(groceries, account, 10_000, currency: "EUR"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("accountAmount").GetInt64().ShouldBe(42_717);
        body.GetProperty("baseAmount").GetInt64().ShouldBe(10_869);
    }

    [Fact]
    public async Task Should_Return503_When_RatesMissing()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");

        using var response = await PostAsync(client, Body(groceries, account, 10_000, currency: "EUR"));

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("rates.unavailable");
        (await ListAsync(client, AccountsPath))[account].GetProperty("inUse").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Should_ReturnPositiveAmount_When_ConvertedAmountIsZero()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");

        using var response = await PostAsync(client, Body(groceries, account, 10_000, currency: "EUR", rate: 1e-9));

        await ShouldHaveFieldErrorsAsync(response, ("rate", "validation.positiveAmount"));
        (await ListAsync(client, AccountsPath))[account].GetProperty("inUse").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Should_ReturnInvalidRate_When_ConvertedAmountAboveMax()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "EUR");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");

        using var response = await PostAsync(client, Body(groceries, account, MinorUnits.MaxAmount, currency: "PLN", rate: 2));

        await ShouldHaveFieldErrorsAsync(response, ("rate", "validation.invalidValue"));
        (await ListAsync(client, AccountsPath))[account].GetProperty("inUse").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Should_ReturnFieldErrors_When_ReferencesInvalid()
    {
        using var owner = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(owner, "PLN");
        var archived = await CreateAccountAsync(owner, "PLN", archived: true);
        var foreignAccount = await CreateAccountAsync(other, "PLN");
        var groceries = await CategoryIdAsync(owner, "Groceries", "expense");
        var salary = await CategoryIdAsync(owner, "Salary", "income");
        var transfers = await CategoryIdAsync(owner, "Transfers", "expense");
        var foreignCategory = await CategoryIdAsync(other, "Groceries", "expense");

        using var foreignCategoryResponse = await PostAsync(owner, Body(foreignCategory, account, 100));
        using var transferResponse = await PostAsync(owner, Body(transfers, account, 100));
        using var mismatchResponse = await PostAsync(owner, Body(salary, account, 100));
        using var unknownCategoryResponse = await PostAsync(owner, Body(Guid.NewGuid(), account, 100));
        using var foreignAccountResponse = await PostAsync(owner, Body(groceries, foreignAccount, 100));
        using var archivedResponse = await PostAsync(owner, Body(groceries, archived, 100));
        using var bothResponse = await PostAsync(owner, Body(transfers, archived, 100, kind: "income"));
        using var invalidKindResponse = await PostAsync(owner, Body(salary, account, 100, kind: "transfer"));

        await ShouldHaveFieldErrorsAsync(foreignCategoryResponse, ("categoryId", "validation.unknownCategory"));
        await ShouldHaveFieldErrorsAsync(transferResponse, ("categoryId", "validation.unknownCategory"));
        await ShouldHaveFieldErrorsAsync(mismatchResponse, ("categoryId", "validation.categoryKindMismatch"));
        await ShouldHaveFieldErrorsAsync(unknownCategoryResponse, ("categoryId", "validation.unknownCategory"));
        await ShouldHaveFieldErrorsAsync(foreignAccountResponse, ("accountId", "validation.unknownAccount"));
        await ShouldHaveFieldErrorsAsync(archivedResponse, ("accountId", "validation.archivedAccount"));
        await ShouldHaveFieldErrorsAsync(
            bothResponse,
            ("categoryId", "validation.unknownCategory"),
            ("accountId", "validation.archivedAccount"));
        await ShouldHaveFieldErrorsAsync(invalidKindResponse, ("kind", "validation.invalidValue"));
        (await ListAsync(owner, AccountsPath))[account].GetProperty("inUse").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Should_GetTransaction_When_Owned()
    {
        using var owner = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(owner, "PLN");
        var groceries = await CategoryIdAsync(owner, "Groceries", "expense");
        using var created = await PostAsync(owner, Body(groceries, account, 990, title: "Milk", note: "Shop"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = createdBody.GetProperty("id").GetGuid();

        using var owned = await owner.Http.GetAsync($"{TransactionsPath}/{id}", Ct);
        using var foreign = await other.Http.GetAsync($"{TransactionsPath}/{id}", Ct);
        using var unknown = await owner.Http.GetAsync($"{TransactionsPath}/{Guid.NewGuid()}", Ct);

        owned.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonElement.DeepEquals(await owned.Content.ReadFromJsonAsync<JsonElement>(Ct), createdBody).ShouldBeTrue();
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_NotFail_When_CategoryDeletedWhileTransactionCreated()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await CreateAccountAsync(client, "PLN");
        var created = 0;

        for (var round = 0; round < 20; round++)
        {
            var category = await TestData.AddCategoryAsync(factory, userId);

            var delete = client.Http.SendAsync(
                client.WithXsrf(new HttpRequestMessage(HttpMethod.Delete, $"{CategoriesPath}/{category.Id}")),
                Ct);
            var create = PostAsync(client, Body(category.Id, account, 100));
            await Task.WhenAll(delete, create);
            using var deleteResponse = await delete;
            using var createResponse = await create;

            deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            createResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);
            if (createResponse.StatusCode == HttpStatusCode.BadRequest)
            {
                await ShouldHaveFieldErrorsAsync(createResponse, ("categoryId", "validation.unknownCategory"));
            }
            else
            {
                created++;
            }
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Transactions.Where(transaction => transaction.UserId == userId).ToListAsync(Ct);
        stored.Count.ShouldBe(created);
        var categoryIds = await db.Categories.Where(category => category.UserId == userId).Select(category => category.Id).ToListAsync(Ct);
        stored.ShouldAllBe(transaction => categoryIds.Contains(transaction.CategoryId));
    }

    private static Dictionary<string, object?> Body(
        Guid categoryId,
        Guid accountId,
        long amount,
        string kind = "expense",
        string currency = "PLN",
        double? rate = null,
        string? title = null,
        string? note = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["kind"] = kind,
            ["date"] = TransactionDate,
            ["categoryId"] = categoryId,
            ["accountId"] = accountId,
            ["amount"] = amount,
            ["currency"] = currency
        };
        if (rate is not null)
        {
            body["rate"] = rate;
        }

        if (title is not null)
        {
            body["title"] = title;
        }

        if (note is not null)
        {
            body["note"] = note;
        }

        return body;
    }

    private static Dictionary<string, object?> SettingsBody(string baseCurrency) => new()
    {
        ["baseCurrency"] = baseCurrency,
        ["locale"] = "en",
        ["monthStartDay"] = 1,
        ["theme"] = "system",
        ["notifications"] = new { budgetLimit = false, dailyReminder = true, weeklyReport = true, goals = false }
    };

    private static async Task<Guid> CreateAccountAsync(AuthClient client, string currency, long openingBalance = 0, bool archived = false)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = "Account",
            ["type"] = "cash",
            ["currency"] = currency,
            ["openingBalance"] = openingBalance,
            ["archived"] = archived
        };
        var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, AccountsPath) { Content = JsonContent.Create(body) });
        using var response = await client.Http.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CategoryIdAsync(AuthClient client, string name, string kind)
    {
        var categories = await ListAsync(client, CategoriesPath);
        return categories.Single(entry =>
            entry.Value.GetProperty("name").GetString() == name && entry.Value.GetProperty("kind").GetString() == kind).Key;
    }

    private static Task<HttpResponseMessage> PostAsync(AuthClient client, object body) =>
        client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, TransactionsPath) { Content = JsonContent.Create(body) }),
            Ct);

    private static async Task<Dictionary<Guid, JsonElement>> ListAsync(AuthClient client, string path)
    {
        using var response = await client.Http.GetAsync(path, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetGuid());
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task ShouldHaveFieldErrorsAsync(HttpResponseMessage response, params (string Field, string Code)[] expected)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var errors = problem.GetProperty("errors").EnumerateObject()
            .Select(property => (property.Name, property.Value.EnumerateArray().Select(code => code.GetString()!).Single()))
            .ToArray();
        errors.ShouldBe(expected, ignoreOrder: true);
    }
}
