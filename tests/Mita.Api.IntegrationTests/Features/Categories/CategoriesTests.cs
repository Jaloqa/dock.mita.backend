using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Categories;

public sealed class CategoriesTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string CategoriesPath = "/v1/categories";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.GetAsync(CategoriesPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_CreateCategory_When_InputValid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await PostAsync(client, Body(name: "  Pets  "));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe($"/v1/categories/{id}");
        body.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "name", "kind", "color", "system", "inUse"],
            ignoreOrder: true);
        body.GetProperty("name").GetString().ShouldBe("Pets");
        body.GetProperty("kind").GetString().ShouldBe("expense");
        body.GetProperty("color").GetString().ShouldBe("chart-3");
        body.GetProperty("system").ValueKind.ShouldBe(JsonValueKind.Null);
        body.GetProperty("inUse").GetBoolean().ShouldBeFalse();

        var names = Names(await ListAsync(client, "kind=expense"));
        names.ShouldBe(["Groceries", "Transport", "Housing", "Cafes", "Health", "Shopping", "Leisure", "Gifts", "Pets", "Other", "Transfers"]);
    }

    [Fact]
    public async Task Should_ReturnNameTaken_When_NameExistsInKind()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var taken = await PostAsync(client, Body(name: " groceries "));
        using var otherKind = await PostAsync(client, Body(name: "Groceries", kind: "income"));

        taken.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(taken))["name"].ShouldBe(["validation.categoryNameTaken"]);
        otherKind.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Should_CreateOneCategory_When_SameNameRaces()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            using var response = await PostAsync(client, Body(name: index % 2 == 0 ? "Pets" : "PETS"));
            return (response.StatusCode, Body: await response.Content.ReadAsStringAsync(Ct));
        }));

        responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        foreach (var (status, body) in responses.Where(response => response.StatusCode != HttpStatusCode.Created))
        {
            status.ShouldBe(HttpStatusCode.BadRequest);
            using var json = JsonDocument.Parse(body);
            json.RootElement.GetProperty("errors").GetProperty("name")[0].GetString().ShouldBe("validation.categoryNameTaken");
        }
    }

    [Fact]
    public async Task Should_FilterByKind()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        var income = await ListAsync(client, "kind=income");

        income.GetProperty("items").GetArrayLength().ShouldBe(8);
        income.GetProperty("total").GetInt32().ShouldBe(8);
        income.GetProperty("items").EnumerateArray().ShouldAllBe(item => item.GetProperty("kind").GetString() == "income");
        (await QueryErrorsAsync(client, "kind=x"))["kind"].ShouldBe(["validation.invalidValue"]);
        (await QueryErrorsAsync(client, "pageSize=101"))["pageSize"].ShouldBe(["validation.invalidValue"]);
    }

    [Fact]
    public async Task Should_PaginateCategories()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        var page = await ListAsync(client, "page=2&pageSize=5");

        Names(page).ShouldBe(["Shopping", "Leisure", "Gifts", "Other", "Transfers"]);
        page.GetProperty("page").GetInt32().ShouldBe(2);
        page.GetProperty("pageSize").GetInt32().ShouldBe(5);
        page.GetProperty("total").GetInt32().ShouldBe(18);
    }

    [Fact]
    public async Task Should_ReportInUse_When_TransactionUsesCategory()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var category = await TestData.AddCategoryAsync(factory, userId, name: "Used");
        await TestData.AddTransactionAsync(factory, account, category, 100);

        var items = (await ListAsync(client)).GetProperty("items").EnumerateArray().ToList();

        items.Single(item => item.GetProperty("id").GetGuid() == category.Id).GetProperty("inUse").GetBoolean().ShouldBeTrue();
        items.Count(item => item.GetProperty("inUse").GetBoolean()).ShouldBe(1);
    }

    [Fact]
    public async Task Should_ShowNewCategory_When_ListWasCached()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(18);
        (await ListAsync(client, "kind=income")).GetProperty("total").GetInt32().ShouldBe(8);

        using var created = await PostAsync(client, Body(name: "Lottery", kind: "income"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(19);
        (await ListAsync(client, "kind=income")).GetProperty("total").GetInt32().ShouldBe(9);
    }

    [Fact]
    public async Task Should_IsolateUsers()
    {
        using var userA = new AuthClient(factory);
        using var userB = new AuthClient(factory);
        await userA.RegisterNewAsync(Ct);
        await userB.RegisterNewAsync(Ct);
        using var created = await PostAsync(userA, Body(name: "Pets"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var createdId = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();

        var pageA = await ListAsync(userA);
        var pageB = await ListAsync(userB);

        pageA.GetProperty("total").GetInt32().ShouldBe(19);
        pageB.GetProperty("total").GetInt32().ShouldBe(18);
        var idsA = Ids(pageA);
        Ids(pageB).ShouldNotContain(createdId);
        Ids(pageB).Intersect(idsA).ShouldBeEmpty();
        using var sameNameForB = await PostAsync(userB, Body(name: "Pets"));
        sameNameForB.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Should_Return400WithFieldErrors_When_BodyInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await PostAsync(client, new { name = "   ", kind = "transfer", color = "red" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = await ErrorsAsync(response);
        errors["name"].ShouldBe(["validation.required"]);
        errors["kind"].ShouldBe(["validation.invalidValue"]);
        errors["color"].ShouldBe(["validation.invalidValue"]);
        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(18);
    }

    private static Dictionary<string, object?> Body(object? name = null, object? kind = null, object? color = null) => new()
    {
        ["name"] = name ?? "Pets",
        ["kind"] = kind ?? "expense",
        ["color"] = color ?? "chart-3"
    };

    private static List<string?> Names(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString())];

    private static List<Guid> Ids(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    private static Task<HttpResponseMessage> PostAsync(AuthClient client, object body)
    {
        var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, CategoriesPath) { Content = JsonContent.Create(body) });
        return client.Http.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> ListAsync(AuthClient client, string? query = null)
    {
        using var response = await client.Http.GetAsync(query is null ? CategoriesPath : $"{CategoriesPath}?{query}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<Dictionary<string, string[]>> QueryErrorsAsync(AuthClient client, string query)
    {
        using var response = await client.Http.GetAsync($"{CategoriesPath}?{query}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        return await ErrorsAsync(response);
    }

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
