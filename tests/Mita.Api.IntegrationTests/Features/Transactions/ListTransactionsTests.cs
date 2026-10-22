using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Transactions;

public sealed class ListTransactionsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string TransactionsPath = "/v1/transactions";
    private const string AccountsPath = "/v1/accounts";
    private const string CategoriesPath = "/v1/categories";
    private const string DefaultDate = "2026-10-01";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.GetAsync(TransactionsPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_ReturnTransactionPage_When_NoFilters()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var created = await CreateAsync(client, groceries, account, 1_250, title: "Bread");

        var page = await GetPageAsync(client, string.Empty);

        page.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["items", "page", "pageSize", "total", "sum", "currency"],
            ignoreOrder: true);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(20);
        page.GetProperty("total").GetInt32().ShouldBe(1);
        page.GetProperty("sum").GetInt64().ShouldBe(-1_250);
        page.GetProperty("currency").GetString().ShouldBe("PLN");
        var item = page.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("id").GetGuid().ShouldBe(created);
        item.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "kind", "date", "title", "note", "categoryId", "accountId", "amount", "currency", "accountAmount", "rate", "baseAmount", "tags"],
            ignoreOrder: true);
        item.GetProperty("title").GetString().ShouldBe("Bread");
        item.GetProperty("date").GetString().ShouldBe(DefaultDate);
    }

    [Fact]
    public async Task Should_SortByDateThenInsertion()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var t1 = await CreateAsync(client, groceries, account, 100, date: "2026-10-01");
        var t2 = await CreateAsync(client, groceries, account, 100, date: "2026-10-02");
        var t3 = await CreateAsync(client, groceries, account, 100, date: "2026-10-02");

        (await IdsAsync(client, string.Empty)).ShouldBe([t3, t2, t1]);
        (await IdsAsync(client, "sort=-date")).ShouldBe([t3, t2, t1]);
        (await IdsAsync(client, "sort=date")).ShouldBe([t1, t2, t3]);
    }

    [Fact]
    public async Task Should_SortByBaseAmount_When_SortIsAmount()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, DefaultDate, 4.2717m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var euro = await CreateAsync(client, groceries, account, 1_000, currency: "EUR");
        var olderZloty = await CreateAsync(client, groceries, account, 2_000, date: "2026-09-30");
        var newerZloty = await CreateAsync(client, groceries, account, 2_000);

        var page = await GetPageAsync(client, "sort=-amount");

        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("baseAmount").GetInt64()).ShouldBe([4_272, 2_000, 2_000]);
        (await IdsAsync(client, "sort=-amount")).ShouldBe([euro, newerZloty, olderZloty]);
        (await IdsAsync(client, "sort=amount")).ShouldBe([newerZloty, olderZloty, euro]);
    }

    [Fact]
    public async Task Should_Filter_ByKindCategoryAccountAndDates()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var first = await CreateAccountAsync(client, "PLN");
        var second = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var transport = await CategoryIdAsync(client, "Transport", "expense");
        var salary = await CategoryIdAsync(client, "Salary", "income");
        var food = await CreateAsync(client, groceries, first, 100, date: "2026-10-01");
        var bus = await CreateAsync(client, transport, second, 200, date: "2026-10-05");
        var pay = await CreateAsync(client, salary, first, 300, kind: "income", date: "2026-10-10");

        (await IdsAsync(client, "kind=income")).ShouldBe([pay]);
        (await IdsAsync(client, "kind=expense")).ShouldBe([bus, food]);
        (await IdsAsync(client, $"categoryId={groceries}")).ShouldBe([food]);
        (await IdsAsync(client, $"accountId={first}")).ShouldBe([pay, food]);
        (await IdsAsync(client, "from=2026-10-05")).ShouldBe([pay, bus]);
        (await IdsAsync(client, "to=2026-10-05")).ShouldBe([bus, food]);
        (await IdsAsync(client, "from=2026-10-05&to=2026-10-05")).ShouldBe([bus]);
        (await IdsAsync(client, $"kind=expense&accountId={first}&categoryId={groceries}&from=2026-10-01&to=2026-10-01")).ShouldBe([food]);
        (await IdsAsync(client, $"kind=income&accountId={second}")).ShouldBeEmpty();

        var reversed = await GetPageAsync(client, "from=2026-10-10&to=2026-10-01");
        reversed.GetProperty("items").GetArrayLength().ShouldBe(0);
        reversed.GetProperty("total").GetInt32().ShouldBe(0);
        reversed.GetProperty("sum").GetInt64().ShouldBe(0);

        var expenses = await GetPageAsync(client, "kind=expense");
        expenses.GetProperty("sum").GetInt64().ShouldBe(-300);

        using var invalid = await client.Http.GetAsync($"{TransactionsPath}?categoryId=abc&accountId=abc&kind=x", Ct);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await invalid.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").EnumerateObject().Select(property => property.Name)
            .ShouldBe(["categoryId", "accountId", "kind"], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_MatchSearch_InTitleNoteAndCategoryName()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var cafes = await CategoryIdAsync(client, "Cafes", "expense");
        var gifts = await CategoryIdAsync(client, "Gifts", "expense");
        var bread = await CreateAsync(client, cafes, account, 100, title: "Bread");
        var cake = await CreateAsync(client, gifts, account, 100, title: "Cake", note: "Birthday cake");
        var weekly = await CreateAsync(client, groceries, account, 100, title: "Weekly");

        (await IdsAsync(client, "search=BREAD")).ShouldBe([bread]);
        (await IdsAsync(client, "search=birthday")).ShouldBe([cake]);
        (await IdsAsync(client, "search=groc")).ShouldBe([weekly]);
        (await IdsAsync(client, $"search={Uri.EscapeDataString("  ")}")).ShouldBe([weekly, cake, bread]);
        (await IdsAsync(client, $"search={Uri.EscapeDataString(" Cake ")}")).ShouldBe([cake]);
    }

    [Fact]
    public async Task Should_MatchSearch_CaseInsensitively_When_TextIsNotAscii()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var shop = await CreateAsync(client, groceries, account, 100, title: "Żabka");
        var bread = await CreateAsync(client, groceries, account, 100, title: "Хліб");
        var milk = await CreateAsync(client, groceries, account, 100, title: "Молоко");

        (await IdsAsync(client, $"search={Uri.EscapeDataString("żab")}")).ShouldBe([shop]);
        (await IdsAsync(client, $"search={Uri.EscapeDataString("ŻAB")}")).ShouldBe([shop]);
        (await IdsAsync(client, $"search={Uri.EscapeDataString("хліб")}")).ShouldBe([bread]);
        (await IdsAsync(client, $"search={Uri.EscapeDataString("МОЛ")}")).ShouldBe([milk]);
    }

    [Fact]
    public async Task Should_MatchAmountPrefix_When_SearchLooksLikeAmount()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var small = await CreateAsync(client, groceries, account, 1_250);
        var round = await CreateAsync(client, groceries, account, 12_000);
        var large = await CreateAsync(client, groceries, account, 31_250);

        (await IdsAsync(client, "search=12")).ShouldBe([round, small]);
        (await IdsAsync(client, $"search={Uri.EscapeDataString("12,5")}")).ShouldBe([small]);
        (await IdsAsync(client, "search=312")).ShouldBe([large]);
        (await IdsAsync(client, "search=12.505")).ShouldBeEmpty();
        (await IdsAsync(client, "search=120.00")).ShouldBe([round]);
        (await IdsAsync(client, "search=12.")).ShouldBe([small]);
    }

    [Fact]
    public async Task Should_MatchAmountSearch_InCurrencyDigits()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var category = await TestData.AddCategoryAsync(factory, userId);
        var jpy = (await TestData.AddTransactionAsync(
            factory, await TestData.AddAccountAsync(factory, userId, Currency.JPY), category, 1_500, "2026-10-01")).Id;
        var kwd = (await TestData.AddTransactionAsync(
            factory, await TestData.AddAccountAsync(factory, userId, Currency.KWD), category, 12_500, "2026-10-02")).Id;
        var pln = (await TestData.AddTransactionAsync(
            factory, await TestData.AddAccountAsync(factory, userId), category, 1_250, "2026-10-03")).Id;

        (await IdsAsync(client, "search=1500")).ShouldBe([jpy]);
        (await IdsAsync(client, "search=15.00")).ShouldBeEmpty();
        (await IdsAsync(client, "search=12.5")).ShouldBe([pln, kwd]);
        (await IdsAsync(client, $"search={Uri.EscapeDataString("12,500")}")).ShouldBe([kwd]);
        (await IdsAsync(client, $"search={Uri.EscapeDataString("12,5")}")).ShouldContain(pln);
    }

    [Fact]
    public async Task Should_TreatSearchLiterally_When_ContainsWildcards()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var juice = await CreateAsync(client, groceries, account, 100, title: "100% juice");
        var underscore = await CreateAsync(client, groceries, account, 100, title: "a_b");
        var backslash = await CreateAsync(client, groceries, account, 100, title: "back\\slash");
        await CreateAsync(client, groceries, account, 100, title: "plain");

        (await IdsAsync(client, $"search={Uri.EscapeDataString("%")}")).ShouldBe([juice]);
        (await IdsAsync(client, $"search={Uri.EscapeDataString("_")}")).ShouldBe([underscore]);
        (await IdsAsync(client, $"search={Uri.EscapeDataString("\\")}")).ShouldBe([backslash]);
    }

    [Fact]
    public async Task Should_SumSignedBaseAmounts_OverAllPages()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var salary = await CategoryIdAsync(client, "Salary", "income");
        await CreateAsync(client, salary, account, 5_000, kind: "income");
        await CreateAsync(client, groceries, account, 1_200);

        foreach (var number in new[] { 1, 2 })
        {
            var page = await GetPageAsync(client, $"pageSize=1&page={number}");

            page.GetProperty("items").GetArrayLength().ShouldBe(1);
            page.GetProperty("page").GetInt32().ShouldBe(number);
            page.GetProperty("pageSize").GetInt32().ShouldBe(1);
            page.GetProperty("total").GetInt32().ShouldBe(2);
            page.GetProperty("sum").GetInt64().ShouldBe(3_800);
        }
    }

    [Fact]
    public async Task Should_IsolateUsers()
    {
        using var owner = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var ownerAccount = await CreateAccountAsync(owner, "PLN");
        var otherAccount = await CreateAccountAsync(other, "PLN");
        var ownerGroceries = await CategoryIdAsync(owner, "Groceries", "expense");
        var otherGroceries = await CategoryIdAsync(other, "Groceries", "expense");
        var owned = await CreateAsync(owner, ownerGroceries, ownerAccount, 100, title: "Shared title");
        await CreateAsync(other, otherGroceries, otherAccount, 700, title: "Shared title");

        (await IdsAsync(owner, string.Empty)).ShouldBe([owned]);
        (await IdsAsync(owner, "search=shared")).ShouldBe([owned]);
        (await IdsAsync(owner, $"accountId={otherAccount}")).ShouldBeEmpty();
        (await IdsAsync(owner, $"categoryId={otherGroceries}")).ShouldBeEmpty();
        (await GetPageAsync(owner, string.Empty)).GetProperty("sum").GetInt64().ShouldBe(-100);
    }

    [Fact]
    public async Task Should_FilterByAnyTag_When_TagIdsGiven()
    {
        using var client = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var otherAccount = await CreateAccountAsync(other, "PLN");
        var otherGroceries = await CategoryIdAsync(other, "Groceries", "expense");
        var t1 = await CreateAsync(client, groceries, account, 100, tags: ["trip"]);
        var t2 = await CreateAsync(client, groceries, account, 100, tags: ["food"]);
        await CreateAsync(client, groceries, account, 100);
        await CreateAsync(other, otherGroceries, otherAccount, 100, tags: ["secret"]);
        var trip = await TagIdAsync(client, "trip");
        var food = await TagIdAsync(client, "food");
        var foreign = await TagIdAsync(other, "secret");

        (await IdsAsync(client, $"tagIds={trip}")).ShouldBe([t1]);
        (await IdsAsync(client, $"tagIds={trip},{food}")).ShouldBe([t2, t1]);
        (await IdsAsync(client, $"tagIds={trip},{food}&kind=income")).ShouldBeEmpty();
        var foreignPage = await GetPageAsync(client, $"tagIds={foreign}");
        foreignPage.GetProperty("items").GetArrayLength().ShouldBe(0);
        foreignPage.GetProperty("total").GetInt32().ShouldBe(0);

        using var invalid = await client.Http.GetAsync($"{TransactionsPath}?tagIds=abc", Ct);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_CountTransactionOnce_When_ItHasSeveralFilteredTags()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var t1 = await CreateAsync(client, groceries, account, 1_000, tags: ["trip", "food"]);
        await CreateAsync(client, groceries, account, 500, tags: ["food"]);
        var trip = await TagIdAsync(client, "trip");
        var food = await TagIdAsync(client, "food");

        var page = await GetPageAsync(client, $"tagIds={trip},{food}");

        page.GetProperty("total").GetInt32().ShouldBe(2);
        page.GetProperty("sum").GetInt64().ShouldBe(-1_500);
        page.GetProperty("items").EnumerateArray().Count(item => item.GetProperty("id").GetGuid() == t1).ShouldBe(1);
    }

    [Fact]
    public async Task Should_MatchSearch_InTagNames()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var account = await CreateAccountAsync(client, "PLN");
        var groceries = await CategoryIdAsync(client, "Groceries", "expense");
        var hotel = await CreateAsync(client, groceries, account, 100, title: "Hotel", tags: ["Barcelona"]);
        var juice = await CreateAsync(client, groceries, account, 100, title: "Juice", tags: ["100%"]);
        await CreateAsync(client, groceries, account, 100, title: "Plain");

        (await IdsAsync(client, "search=barc")).ShouldBe([hotel]);
        (await IdsAsync(client, $"search={Uri.EscapeDataString("%")}")).ShouldBe([juice]);
    }

    private static async Task<Guid> TagIdAsync(AuthClient client, string name) =>
        (await GetPageAsync(client, "pageSize=100")).GetProperty("items").EnumerateArray()
            .SelectMany(item => item.GetProperty("tags").EnumerateArray())
            .First(tag => tag.GetProperty("name").GetString() == name)
            .GetProperty("id").GetGuid();

    private static async Task<JsonElement> GetPageAsync(AuthClient client, string query)
    {
        using var response = await client.Http.GetAsync(query.Length == 0 ? TransactionsPath : $"{TransactionsPath}?{query}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid[]> IdsAsync(AuthClient client, string query) =>
        [.. (await GetPageAsync(client, query)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    private static async Task<Guid> CreateAsync(
        AuthClient client,
        Guid categoryId,
        Guid accountId,
        long amount,
        string kind = "expense",
        string date = DefaultDate,
        string currency = "PLN",
        string? title = null,
        string? note = null,
        string[]? tags = null)
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

        if (tags is not null)
        {
            body["tags"] = tags;
        }

        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, TransactionsPath) { Content = JsonContent.Create(body) }),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateAccountAsync(AuthClient client, string currency)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = $"Account {Guid.NewGuid():N}"[..20],
            ["type"] = "cash",
            ["currency"] = currency,
            ["openingBalance"] = 0,
            ["archived"] = false
        };
        using var response = await client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, AccountsPath) { Content = JsonContent.Create(body) }),
            Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CategoryIdAsync(AuthClient client, string name, string kind)
    {
        using var response = await client.Http.GetAsync(CategoriesPath, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name && item.GetProperty("kind").GetString() == kind)
            .GetProperty("id").GetGuid();
    }
}
