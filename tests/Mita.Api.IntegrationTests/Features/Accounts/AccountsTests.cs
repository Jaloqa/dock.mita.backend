using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Accounts;

public sealed class AccountsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string AccountsPath = "/v1/accounts";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.GetAsync(AccountsPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_ReturnEmptyPage_When_UserHasNoAccounts()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        var page = await ListAsync(client);

        page.GetProperty("items").GetArrayLength().ShouldBe(0);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(100);
        page.GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Should_CreateAccount_When_InputValid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await PostAsync(client, Body(name: "  Main card  ", openingBalance: 12345));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        response.Headers.Location!.ToString().ShouldBe($"/v1/accounts/{body.GetProperty("id").GetGuid()}");
        body.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "name", "type", "currency", "openingBalance", "balance", "cardLast4", "archived", "inUse", "linkedGoals"],
            ignoreOrder: true);
        body.GetProperty("name").GetString().ShouldBe("Main card");
        body.GetProperty("type").GetString().ShouldBe("card");
        body.GetProperty("currency").GetString().ShouldBe("EUR");
        body.GetProperty("openingBalance").GetInt64().ShouldBe(12345);
        body.GetProperty("balance").GetInt64().ShouldBe(12345);
        body.GetProperty("cardLast4").GetString().ShouldBe("1234");
        body.GetProperty("archived").GetBoolean().ShouldBeFalse();
        body.GetProperty("inUse").GetBoolean().ShouldBeFalse();
        body.GetProperty("linkedGoals").GetInt32().ShouldBe(0);

        var page = await ListAsync(client);
        page.GetProperty("total").GetInt32().ShouldBe(1);
        page.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(body.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Should_CreateAccount_When_CurrencyIsJpy()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await PostAsync(client, Body(currency: "JPY"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("currency").GetString().ShouldBe("JPY");
    }

    [Fact]
    public async Task Should_DropCardLast4_When_TypeIsNotCard()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await PostAsync(client, Body(type: "cash", cardLast4: "1234"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("cardLast4").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_StoreArchived_When_CreatedArchived()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await PostAsync(client, Body(archived: true));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("archived").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ListInCreationOrder_And_IsolateUsers()
    {
        using var userA = new AuthClient(factory);
        using var userB = new AuthClient(factory);
        await userA.RegisterNewAsync(Ct);
        await userB.RegisterNewAsync(Ct);
        foreach (var name in new[] { "First", "Second", "Third" })
        {
            using var created = await PostAsync(userA, Body(name: name));
            created.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var pageA = await ListAsync(userA);
        var pageB = await ListAsync(userB);

        pageA.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString())
            .ShouldBe(["First", "Second", "Third"]);
        pageA.GetProperty("total").GetInt32().ShouldBe(3);
        pageB.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Should_ComputeBalanceFromMovements()
    {
        using var client = new AuthClient(factory);
        var credentials = await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client, credentials);
        var from = await TestData.AddAccountAsync(factory, userId, openingBalance: 10_000);
        var to = await TestData.AddAccountAsync(factory, userId, openingBalance: 500);
        var untouched = await TestData.AddAccountAsync(factory, userId, openingBalance: 7);
        await TestData.AddTransferAsync(factory, from, to, 2500);

        var items = (await ListAsync(client)).GetProperty("items").EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetGuid());

        items[from.Id].GetProperty("balance").GetInt64().ShouldBe(7500);
        items[from.Id].GetProperty("inUse").GetBoolean().ShouldBeTrue();
        items[to.Id].GetProperty("balance").GetInt64().ShouldBe(3000);
        items[to.Id].GetProperty("inUse").GetBoolean().ShouldBeTrue();
        items[untouched.Id].GetProperty("balance").GetInt64().ShouldBe(7);
        items[untouched.Id].GetProperty("inUse").GetBoolean().ShouldBeFalse();
    }

    [Theory]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("page=abc", "page")]
    public async Task Should_Return400_When_PagingInvalid(string query, string parameter)
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await client.Http.GetAsync($"{AccountsPath}?{query}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("errors").GetProperty(parameter)[0].GetString().ShouldBe("validation.invalidValue");
    }

    [Fact]
    public async Task Should_PaginateAccounts()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        foreach (var name in new[] { "A", "B", "C" })
        {
            using var created = await PostAsync(client, Body(name: name));
            created.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var page = await ListAsync(client, "page=2&pageSize=2");

        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()).ShouldBe(["C"]);
        page.GetProperty("total").GetInt32().ShouldBe(3);
        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("pageSize").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Should_ShowNewAccount_When_ListWasCached()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(0);

        using var created = await PostAsync(client, Body());
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Return400WithFieldErrors_When_BodyInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await PostAsync(client, Body(name: "   ", openingBalance: "100"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var errors = problem.GetProperty("errors");
        errors.GetProperty("name")[0].GetString().ShouldBe("validation.required");
        errors.GetProperty("openingBalance")[0].GetString().ShouldBe("validation.invalidValue");
        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Should_Reject_When_XsrfMissing()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, AccountsPath) { Content = JsonContent.Create(Body()) };

        using var response = await client.Http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static Dictionary<string, object?> Body(
        object? name = null,
        object? type = null,
        object? currency = null,
        object? openingBalance = null,
        object? cardLast4 = null,
        object? archived = null) => new()
    {
        ["name"] = name ?? "Main",
        ["type"] = type ?? "card",
        ["currency"] = currency ?? "EUR",
        ["openingBalance"] = openingBalance ?? 0,
        ["cardLast4"] = cardLast4 ?? (type is null or "card" ? "1234" : null),
        ["archived"] = archived ?? false
    };

    private static Task<HttpResponseMessage> PostAsync(AuthClient client, object body)
    {
        var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, AccountsPath) { Content = JsonContent.Create(body) });
        return client.Http.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> ListAsync(AuthClient client, string? query = null)
    {
        using var response = await client.Http.GetAsync(query is null ? AccountsPath : $"{AccountsPath}?{query}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client, TestCredentials credentials)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("email").GetString().ShouldBe(credentials.Email);
        return body.GetProperty("id").GetGuid();
    }
}
