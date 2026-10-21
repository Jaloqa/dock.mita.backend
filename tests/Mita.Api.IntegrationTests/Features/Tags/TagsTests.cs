using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Tags;

public sealed class TagsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string TagsPath = "/v1/tags";
    private const string TransactionsPath = "/v1/transactions";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);
        var id = Guid.NewGuid();

        using var list = await client.Http.GetAsync(TagsPath, Ct);
        using var rename = await client.Http.PutAsJsonAsync($"{TagsPath}/{id}", new { name = "x" }, Ct);
        using var delete = await client.Http.DeleteAsync($"{TagsPath}/{id}", Ct);

        list.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        rename.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        delete.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_ListTagsWithUsage_OrderedByName()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var user = await UserAsync(client);
        var beta = await TestData.AddTagAsync(factory, user.Id, "beta");
        await TestData.AddTagAsync(factory, user.Id, "Alpha");
        await TagTransactionsAsync(user, 2, beta);

        var page = await ListAsync(client);

        page.GetProperty("pageSize").GetInt32().ShouldBe(100);
        page.GetProperty("total").GetInt32().ShouldBe(2);
        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("name").GetString()).ShouldBe(["Alpha", "beta"]);
        items.Select(item => item.GetProperty("usage").GetInt32()).ShouldBe([0, 2]);
        items[0].EnumerateObject().Select(property => property.Name).ShouldBe(["id", "name", "usage"], ignoreOrder: true);
    }

    [Fact]
    public async Task Should_FilterTagsBySearch()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var user = await UserAsync(client);
        foreach (var name in new[] { "Alpha", "beta", "100%" })
        {
            await TestData.AddTagAsync(factory, user.Id, name);
        }

        (await NamesAsync(client, "search=AL")).ShouldBe(["Alpha"]);
        (await NamesAsync(client, "search=%25")).ShouldBe(["100%"]);
        (await NamesAsync(client, "search=%20%20")).ShouldBe(["100%", "Alpha", "beta"]);
    }

    [Fact]
    public async Task Should_PageTags()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var user = await UserAsync(client);
        foreach (var name in new[] { "a", "b", "c" })
        {
            await TestData.AddTagAsync(factory, user.Id, name);
        }

        var page = await ListAsync(client, "pageSize=2&page=2");

        page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()).ShouldBe(["c"]);
        page.GetProperty("total").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task Should_ReturnInvalidValue_When_PagingOutOfRange()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var tooLarge = await client.Http.GetAsync($"{TagsPath}?pageSize=101", Ct);
        using var zero = await client.Http.GetAsync($"{TagsPath}?page=0", Ct);

        tooLarge.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(tooLarge))["pageSize"].ShouldBe(["validation.invalidValue"]);
        zero.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(zero))["page"].ShouldBe(["validation.invalidValue"]);
    }

    [Fact]
    public async Task Should_RenameTag_When_NameFree()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var user = await UserAsync(client);
        var tag = await TestData.AddTagAsync(factory, user.Id, "old");
        var transaction = (await TagTransactionsAsync(user, 1, tag)).Single();

        using var response = await PutAsync(client, tag.Id, new { name = "  new  " });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("id").GetGuid().ShouldBe(tag.Id);
        body.GetProperty("name").GetString().ShouldBe("new");
        body.GetProperty("usage").GetInt32().ShouldBe(1);
        (await TransactionTagNamesAsync(client, transaction.Id)).ShouldBe(["new"]);
    }

    [Fact]
    public async Task Should_AllowCaseChange_When_RenamingOwnTag()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var user = await UserAsync(client);
        var tag = await TestData.AddTagAsync(factory, user.Id, "trip");

        using var response = await PutAsync(client, tag.Id, new { name = "Trip" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("name").GetString().ShouldBe("Trip");
    }

    [Fact]
    public async Task Should_ReturnNameTaken_When_RenamingToExistingTag()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var user = await UserAsync(client);
        await TestData.AddTagAsync(factory, user.Id, "food");
        var tag = await TestData.AddTagAsync(factory, user.Id, "meals");

        using var response = await PutAsync(client, tag.Id, new { name = " FOOD " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = await ErrorsAsync(response);
        errors.Keys.ShouldBe(["name"]);
        errors["name"].ShouldBe(["validation.tagNameTaken"]);
    }

    [Fact]
    public async Task Should_ReturnOnlyRequiredError_When_NameBlank()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var user = await UserAsync(client);
        var tag = await TestData.AddTagAsync(factory, user.Id, "food");

        using var response = await PutAsync(client, tag.Id, new { name = "   " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response))["name"].ShouldBe(["validation.required"]);
    }

    [Fact]
    public async Task Should_DeleteTag_AndRemoveItFromTransactions()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var user = await UserAsync(client);
        var tag = await TestData.AddTagAsync(factory, user.Id, "gone");
        var keep = await TestData.AddTagAsync(factory, user.Id, "keep");
        var transaction = (await TagTransactionsAsync(user, 1, tag, keep)).Single();

        using var response = await DeleteAsync(client, tag.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await TransactionTagNamesAsync(client, transaction.Id)).ShouldBe(["keep"]);
        (await NamesAsync(client, null)).ShouldBe(["keep"]);
    }

    [Fact]
    public async Task Should_Return404_When_TagBelongsToAnotherUser()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var tag = await TestData.AddTagAsync(factory, (await UserAsync(owner)).Id, "private");

        using var put = await PutAsync(intruder, tag.Id, new { name = "stolen" });
        using var delete = await DeleteAsync(intruder, tag.Id);
        using var unknownPut = await PutAsync(intruder, Guid.NewGuid(), new { name = "stolen" });
        using var unknownDelete = await DeleteAsync(intruder, Guid.NewGuid());

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownPut.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await NamesAsync(owner, null)).ShouldBe(["private"]);
    }

    [Fact]
    public async Task Should_ReturnValidationError_When_BodyInvalidOnUnknownId()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await PutAsync(client, Guid.NewGuid(), new { name = 5 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response))["name"].ShouldBe(["validation.invalidValue"]);
    }

    [Fact]
    public async Task Should_NotFail_When_TagDeletedWhileTransactionSaved()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var user = await UserAsync(client);
        var account = await TestData.AddAccountAsync(factory, user.Id);
        var category = await TestData.AddCategoryAsync(factory, user.Id);

        for (var round = 0; round < 20; round++)
        {
            var name = $"T{round}";
            var tag = await TestData.AddTagAsync(factory, user.Id, name);
            await TagTransactionsAsync(user, 1, tag);
            using var start = new Barrier(2);

            var deleting = Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait(Ct);
                    return DeleteAsync(client, tag.Id);
                },
                Ct,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
            var creating = Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait(Ct);
                    return PostTransactionAsync(client, account.Id, category.Id, name);
                },
                Ct,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
            var responses = await Task.WhenAll(deleting, creating);

            responses[0].StatusCode.ShouldBe(HttpStatusCode.NoContent);
            responses[1].StatusCode.ShouldBe(HttpStatusCode.Created);
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dangling = await db.Database
            .SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM transaction_tags link
                LEFT JOIN tags ON tags."Id" = link."TagId"
                WHERE tags."Id" IS NULL
                """)
            .SingleAsync(Ct);
        dangling.ShouldBe(0);
    }

    [Fact]
    public async Task Should_AnswerOkOrNotFound_When_TagRenamedWhileDeleted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var user = await UserAsync(client);

        for (var round = 0; round < 100; round++)
        {
            var tag = await TestData.AddTagAsync(factory, user.Id, $"T{round}");
            await TagTransactionsAsync(user, 1, tag);
            using var start = new Barrier(2);

            var deleting = Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait(Ct);
                    return DeleteAsync(client, tag.Id);
                },
                Ct,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
            var renaming = Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait(Ct);
                    return PutAsync(client, tag.Id, new { name = $"Renamed{round}" });
                },
                Ct,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
            var responses = await Task.WhenAll(deleting, renaming);

            responses[0].StatusCode.ShouldBe(HttpStatusCode.NoContent);
            responses[1].StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private async Task<List<TransactionRef>> TagTransactionsAsync(UserRef user, int count, params Mita.Domain.Tags.Tag[] tags)
    {
        var account = await TestData.AddAccountAsync(factory, user.Id);
        var category = await TestData.AddCategoryAsync(factory, user.Id);
        var transactions = new List<TransactionRef>();
        for (var index = 0; index < count; index++)
        {
            var transaction = await TestData.AddTransactionAsync(factory, account, category, 1_000);
            await TestData.TagTransactionAsync(factory, transaction, tags);
            transactions.Add(new TransactionRef(transaction.Id));
        }

        return transactions;
    }

    private static Task<HttpResponseMessage> PostTransactionAsync(AuthClient client, Guid accountId, Guid categoryId, string tag)
    {
        var body = new Dictionary<string, object?>
        {
            ["kind"] = "expense",
            ["date"] = "2026-03-02",
            ["categoryId"] = categoryId,
            ["accountId"] = accountId,
            ["amount"] = 1_000,
            ["currency"] = "PLN",
            ["tags"] = new[] { tag }
        };
        return client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Post, TransactionsPath) { Content = JsonContent.Create(body) }),
            Ct);
    }

    private static async Task<List<string?>> TransactionTagNamesAsync(AuthClient client, Guid transactionId)
    {
        using var response = await client.Http.GetAsync(TransactionsPath, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return [.. page.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == transactionId)
            .GetProperty("tags").EnumerateArray().Select(tag => tag.GetProperty("name").GetString())];
    }

    private static async Task<List<string?>> NamesAsync(AuthClient client, string? query)
    {
        var page = await ListAsync(client, query);
        return [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString())];
    }

    private static async Task<JsonElement> ListAsync(AuthClient client, string? query = null)
    {
        using var response = await client.Http.GetAsync(query is null ? TagsPath : $"{TagsPath}?{query}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<HttpResponseMessage> PutAsync(AuthClient client, Guid id, object body) =>
        client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, $"{TagsPath}/{id}") { Content = JsonContent.Create(body) }),
            Ct);

    private static Task<HttpResponseMessage> DeleteAsync(AuthClient client, Guid id) =>
        client.Http.SendAsync(client.WithXsrf(new HttpRequestMessage(HttpMethod.Delete, $"{TagsPath}/{id}")), Ct);

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return problem.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
    }

    private static async Task<UserRef> UserAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return new UserRef((await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid());
    }

    private sealed record UserRef(Guid Id);

    private sealed record TransactionRef(Guid Id);
}
