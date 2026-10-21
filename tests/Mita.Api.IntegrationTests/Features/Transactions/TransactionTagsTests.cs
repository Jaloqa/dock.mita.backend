using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Transactions;

public sealed class TransactionTagsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string TransactionsPath = "/v1/transactions";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_CreateTransactionWithTags_When_NamesGiven()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var setup = await SetupAsync(client);

        using var response = await PostAsync(client, Body(setup, [" Trip ", "food", "trip"]));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var tags = TagsOf(body);
        tags.Select(tag => tag.Name).ShouldBe(["food", "Trip"]);
        var usage = await UsageAsync(setup.UserId);
        usage.Keys.ShouldBe(["food", "Trip"], ignoreOrder: true);
        usage.Values.ShouldAllBe(count => count == 1);
        tags.Select(tag => tag.Id).ShouldBe(await TagIdsAsync(setup.UserId, "food", "Trip"));
    }

    [Fact]
    public async Task Should_ReuseStoredSpelling_When_NameDiffersInCase()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var setup = await SetupAsync(client);
        var stored = await TestData.AddTagAsync(factory, setup.UserId, "groceries-run");

        using var response = await PostAsync(client, Body(setup, [" GROCERIES-run "]));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tag = TagsOf(await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).Single();
        tag.Name.ShouldBe("groceries-run");
        tag.Id.ShouldBe(stored.Id);
        (await UsageAsync(setup.UserId)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Should_ReturnEmptyTags_When_NoneGiven()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var setup = await SetupAsync(client);

        using var response = await PostAsync(client, Body(setup, null));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("tags").ValueKind.ShouldBe(JsonValueKind.Array);
        TagsOf(body).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_ReplaceTags_When_Updated()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var setup = await SetupAsync(client);
        var id = await CreateAsync(client, Body(setup, ["a", "b"]));

        using var response = await PutAsync(client, id, Body(setup, ["b", "c"]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TagsOf(await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).Select(tag => tag.Name).ShouldBe(["b", "c"]);
        var usage = await UsageAsync(setup.UserId);
        usage["a"].ShouldBe(0);
        usage["b"].ShouldBe(1);
        usage["c"].ShouldBe(1);
    }

    [Fact]
    public async Task Should_ClearTags_When_PutOmitsTags()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var setup = await SetupAsync(client);
        var id = await CreateAsync(client, Body(setup, ["a", "b"]));

        using var response = await PutAsync(client, id, Body(setup, null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TagsOf(await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).ShouldBeEmpty();
        (await UsageAsync(setup.UserId)).Values.ShouldAllBe(count => count == 0);
    }

    [Fact]
    public async Task Should_ReturnTagErrors_When_TagsInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var setup = await SetupAsync(client);

        using var response = await PostAsync(client, Body(setup, Enumerable.Range(1, 11).Select(index => $"t{index}").ToArray()));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").GetProperty("tags").EnumerateArray().Select(code => code.GetString())
            .ShouldBe(["validation.tooManyTags"]);
        (await UsageAsync(setup.UserId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_NotStoreTags_When_TransactionRejected()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var setup = await SetupAsync(client);
        using var other = new AuthClient(factory);
        await other.RegisterNewAsync(Ct);
        var foreign = await CreateAccountAsync(other);

        using var response = await PostAsync(client, Body(setup, ["Trip"]).WithAccount(foreign));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await UsageAsync(setup.UserId)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_CreateOneTag_When_SameNewTagSavedConcurrently()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var setup = await SetupAsync(client);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => PostAsync(client, Body(setup, ["Shared"]))));

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            response.Dispose();
        }

        var usage = await UsageAsync(setup.UserId);
        usage.Keys.ShouldBe(["Shared"]);
        usage["Shared"].ShouldBe(10);
    }

    [Fact]
    public async Task Should_IsolateTagsBetweenUsers()
    {
        using var first = new AuthClient(factory);
        await first.RegisterNewAsync(Ct);
        var firstSetup = await SetupAsync(first);
        using var second = new AuthClient(factory);
        await second.RegisterNewAsync(Ct);
        var secondSetup = await SetupAsync(second);

        using var a = await PostAsync(first, Body(firstSetup, ["Trip"]));
        using var b = await PostAsync(second, Body(secondSetup, ["Trip"]));

        a.StatusCode.ShouldBe(HttpStatusCode.Created);
        b.StatusCode.ShouldBe(HttpStatusCode.Created);
        var firstTag = TagsOf(await a.Content.ReadFromJsonAsync<JsonElement>(Ct)).Single();
        var secondTag = TagsOf(await b.Content.ReadFromJsonAsync<JsonElement>(Ct)).Single();
        secondTag.Id.ShouldNotBe(firstTag.Id);
    }

    private static List<(Guid Id, string Name)> TagsOf(JsonElement transaction) =>
        [.. transaction.GetProperty("tags").EnumerateArray()
            .Select(tag => (tag.GetProperty("id").GetGuid(), tag.GetProperty("name").GetString()!))];

    private async Task<Dictionary<string, int>> UsageAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Tags.AsNoTracking()
            .Where(tag => tag.UserId == userId)
            .Select(tag => new { tag.Name, Usage = db.TransactionTags.Count(link => link.TagId == tag.Id) })
            .ToDictionaryAsync(row => row.Name, row => row.Usage, Ct);
    }

    private async Task<List<Guid>> TagIdsAsync(Guid userId, params string[] names)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.Tags.AsNoTracking().Where(tag => tag.UserId == userId).ToListAsync(Ct);
        return [.. names.Select(name => rows.Single(tag => tag.Name == name).Id)];
    }

    private static async Task<Setup> SetupAsync(AuthClient client)
    {
        var account = await CreateAccountAsync(client);
        using var categories = await client.Http.GetAsync("/v1/categories", Ct);
        var page = await categories.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var category = page.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("kind").GetString() == "expense" && item.GetProperty("name").GetString() == "Groceries")
            .GetProperty("id").GetGuid();
        using var me = await client.Http.GetAsync("/v1/me", Ct);
        var userId = (await me.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        return new Setup(userId, account, category);
    }

    private static async Task<Guid> CreateAccountAsync(AuthClient client)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = "Account",
            ["type"] = "cash",
            ["currency"] = "PLN",
            ["openingBalance"] = 0,
            ["archived"] = false
        };
        var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, "/v1/accounts") { Content = JsonContent.Create(body) });
        using var response = await client.Http.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static TagBody Body(Setup setup, string[]? tags) => new(setup.AccountId, setup.CategoryId, tags);

    private static async Task<Guid> CreateAsync(AuthClient client, TagBody body)
    {
        using var response = await PostAsync(client, body);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> PostAsync(AuthClient client, TagBody body) =>
        client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, TransactionsPath) { Content = JsonContent.Create(body.ToJson()) }),
            Ct);

    private static Task<HttpResponseMessage> PutAsync(AuthClient client, Guid id, TagBody body) =>
        client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, $"{TransactionsPath}/{id}") { Content = JsonContent.Create(body.ToJson()) }),
            Ct);

    private sealed record Setup(Guid UserId, Guid AccountId, Guid CategoryId);

    private sealed record TagBody(Guid AccountId, Guid CategoryId, string[]? Tags)
    {
        public TagBody WithAccount(Guid accountId) => this with { AccountId = accountId };

        public Dictionary<string, object?> ToJson()
        {
            var body = new Dictionary<string, object?>
            {
                ["kind"] = "expense",
                ["date"] = "2026-03-02",
                ["categoryId"] = CategoryId,
                ["accountId"] = AccountId,
                ["amount"] = 1_000,
                ["currency"] = "PLN"
            };
            if (Tags is not null)
            {
                body["tags"] = Tags;
            }

            return body;
        }
    }
}
