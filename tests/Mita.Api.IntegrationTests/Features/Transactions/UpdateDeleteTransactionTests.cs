using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Transactions;

public sealed class UpdateDeleteTransactionTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string TransactionsPath = "/v1/transactions";
    private const string AccountsPath = "/v1/accounts";
    private const string CategoriesPath = "/v1/categories";
    private const string DefaultDate = "2026-03-02";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var put = await client.Http.PutAsJsonAsync($"{TransactionsPath}/{Guid.NewGuid()}", new { }, Ct);
        using var delete = await client.Http.DeleteAsync($"{TransactionsPath}/{Guid.NewGuid()}", Ct);

        put.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        delete.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_UpdateAllFields_When_InputValid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var accountA = await CreateAccountAsync(client, "PLN", openingBalance: 10_000);
        var accountB = await CreateAccountAsync(client, "PLN", openingBalance: 10_000);
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var salary = await CategoryIdAsync(client, "Salary", "income");
        var id = await CreateAsync(client, Body(groceries, accountA, 1_000, title: "Old"));
        (await BalancesAsync(client))[accountA].ShouldBe(9_000);

        using var response = await PutAsync(
            client,
            id,
            Body(salary, accountB, 1_500, kind: "income", date: "2026-04-05", title: " New ", note: "Memo"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("id").GetGuid().ShouldBe(id);
        var balances = await BalancesAsync(client);
        balances[accountA].ShouldBe(10_000);
        balances[accountB].ShouldBe(11_500);

        using var get = await client.Http.GetAsync($"{TransactionsPath}/{id}", Ct);
        var stored = await get.Content.ReadFromJsonAsync<JsonElement>(Ct);
        JsonElement.DeepEquals(stored, body).ShouldBeTrue();
        stored.GetProperty("kind").GetString().ShouldBe("income");
        stored.GetProperty("date").GetString().ShouldBe("2026-04-05");
        stored.GetProperty("title").GetString().ShouldBe("New");
        stored.GetProperty("note").GetString().ShouldBe("Memo");
        stored.GetProperty("categoryId").GetGuid().ShouldBe(salary);
        stored.GetProperty("accountId").GetGuid().ShouldBe(accountB);
        stored.GetProperty("amount").GetInt64().ShouldBe(1_500);
        stored.GetProperty("accountAmount").GetInt64().ShouldBe(1_500);
        stored.GetProperty("baseAmount").GetInt64().ShouldBe(1_500);
    }

    [Fact]
    public async Task Should_RecomputeAmounts_When_UpdateChangesCurrency()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, DefaultDate, 4.2717m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var id = await CreateAsync(client, Body(groceries, account, 100_000));

        using var response = await PutAsync(client, id, Body(groceries, account, 100_000, currency: "EUR"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("currency").GetString().ShouldBe("EUR");
        body.GetProperty("rate").GetDouble().ShouldBe(4.2717);
        body.GetProperty("accountAmount").GetInt64().ShouldBe(427_170);
        body.GetProperty("baseAmount").GetInt64().ShouldBe(427_170);
    }

    [Fact]
    public async Task Should_AllowArchivedAccount_When_TransactionKeepsIt()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var id = await CreateAsync(client, Body(groceries, account, 1_000, title: "Before"));
        await ArchiveAsync(client, account);

        using var response = await PutAsync(client, id, Body(groceries, account, 1_000, title: "After"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("title").GetString().ShouldBe("After");
    }

    [Fact]
    public async Task Should_ReturnArchivedAccount_When_MovingToArchivedAccount()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var archived = await CreateAccountAsync(client, "PLN", archived: true);
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var id = await CreateAsync(client, Body(groceries, account, 1_000));

        using var response = await PutAsync(client, id, Body(groceries, archived, 1_000));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").GetProperty("accountId").EnumerateArray().Select(code => code.GetString()).ShouldBe(["validation.archivedAccount"]);
    }

    [Fact]
    public async Task Should_KeepListPosition_When_Updated()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var older = await CreateAsync(client, Body(groceries, account, 100));
        var newer = await CreateAsync(client, Body(groceries, account, 100));

        using var response = await PutAsync(client, older, Body(groceries, account, 200, title: "Edited"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var list = await client.Http.GetAsync($"{TransactionsPath}?sort=-date", Ct);
        var page = await list.Content.ReadFromJsonAsync<JsonElement>(Ct);
        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldBe([newer, older]);
    }

    [Fact]
    public async Task Should_Return404_When_TransactionBelongsToAnotherUser()
    {
        using var owner = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(owner, "PLN");
        var groceries = await CategoryIdAsync(owner, "Groceries", "expense");
        var id = await CreateAsync(owner, Body(groceries, account, 1_000));
        var otherAccount = await CreateAccountAsync(other, "PLN");
        var otherGroceries = await CategoryIdAsync(other, "Groceries", "expense");

        using var put = await PutAsync(other, id, Body(otherGroceries, otherAccount, 500));
        using var unknownPut = await PutAsync(other, Guid.NewGuid(), Body(otherGroceries, otherAccount, 500));
        using var delete = await DeleteAsync(other, id);
        using var invalid = await PutAsync(other, Guid.NewGuid(), new Dictionary<string, object?>());

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownPut.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var stillThere = await owner.Http.GetAsync($"{TransactionsPath}/{id}", Ct);
        stillThere.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_DeleteTransaction_And_RestoreBalance()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN", openingBalance: 5_000);
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var id = await CreateAsync(client, Body(groceries, account, 1_200));
        using var blocked = await DeleteAsync(client, account, AccountsPath);
        blocked.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        using var response = await DeleteAsync(client, id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await BalancesAsync(client))[account].ShouldBe(5_000);
        var accounts = await ListAsync(client, AccountsPath);
        accounts[account].GetProperty("inUse").GetBoolean().ShouldBeFalse();
        (await ListAsync(client, CategoriesPath))[groceries].GetProperty("inUse").GetBoolean().ShouldBeFalse();
        using var get = await client.Http.GetAsync($"{TransactionsPath}/{id}", Ct);
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var again = await DeleteAsync(client, id);
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var deleteAccount = await DeleteAsync(client, account, AccountsPath);
        deleteAccount.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static Dictionary<string, object?> Body(
        Guid categoryId,
        Guid accountId,
        long amount,
        string kind = "expense",
        string currency = "PLN",
        string date = DefaultDate,
        string? title = null,
        string? note = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["kind"] = kind,
            ["date"] = date,
            ["categoryId"] = categoryId,
            ["accountId"] = accountId,
            ["amount"] = amount,
            ["currency"] = currency
        };
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

    private static async Task<Guid> CreateAsync(AuthClient client, object body)
    {
        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, TransactionsPath) { Content = JsonContent.Create(body) }),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> PutAsync(AuthClient client, Guid id, object body) =>
        client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, $"{TransactionsPath}/{id}") { Content = JsonContent.Create(body) }),
            Ct);

    private static Task<HttpResponseMessage> DeleteAsync(AuthClient client, Guid id, string path = TransactionsPath) =>
        client.Http.SendAsync(client.WithXsrf(new HttpRequestMessage(HttpMethod.Delete, $"{path}/{id}")), Ct);

    private static async Task ArchiveAsync(AuthClient client, Guid account)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = "Account",
            ["type"] = "cash",
            ["currency"] = "PLN",
            ["openingBalance"] = 0,
            ["archived"] = true
        };
        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, $"{AccountsPath}/{account}") { Content = JsonContent.Create(body) }),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

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
        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, AccountsPath) { Content = JsonContent.Create(body) }),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CategoryIdAsync(AuthClient client, string name, string kind)
    {
        var categories = await ListAsync(client, CategoriesPath);
        return categories.Single(entry =>
            entry.Value.GetProperty("name").GetString() == name && entry.Value.GetProperty("kind").GetString() == kind).Key;
    }

    private static async Task<Dictionary<Guid, long>> BalancesAsync(AuthClient client) =>
        (await ListAsync(client, AccountsPath)).ToDictionary(entry => entry.Key, entry => entry.Value.GetProperty("balance").GetInt64());

    private static async Task<Dictionary<Guid, JsonElement>> ListAsync(AuthClient client, string path)
    {
        using var response = await client.Http.GetAsync(path, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("id").GetGuid());
    }
}
