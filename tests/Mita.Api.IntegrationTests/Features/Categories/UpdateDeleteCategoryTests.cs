using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Categories;

public sealed class UpdateDeleteCategoryTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string CategoriesPath = "/v1/categories";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_UpdateNameAndColor_When_SystemCategory()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var other = await FindAsync(client, "Other", "expense");

        using var response = await PutAsync(client, other.GetProperty("id").GetGuid(), Body(name: "Misc", color: "chart-3"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.GetProperty("name").GetString().ShouldBe("Misc");
        body.GetProperty("color").GetString().ShouldBe("chart-3");
        body.GetProperty("system").GetString().ShouldBe("other");
        var listed = await FindAsync(client, "Misc", "expense");
        listed.GetProperty("id").GetGuid().ShouldBe(other.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Should_ReturnKindLocked_When_SystemCategory()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var other = await FindAsync(client, "Other", "expense");

        using var response = await PutAsync(client, other.GetProperty("id").GetGuid(), Body(name: "Other", kind: "income"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response))["kind"].ShouldBe(["validation.categoryKindLocked"]);
    }

    [Fact]
    public async Task Should_ReturnKindLocked_When_CategoryInUse()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var category = await TestData.AddCategoryAsync(factory, userId, name: "Used");
        await TestData.AddTransactionAsync(factory, account, category, 100);

        using var response = await PutAsync(client, category.Id, Body(name: "Used", kind: "income"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response))["kind"].ShouldBe(["validation.categoryKindLocked"]);
    }

    [Fact]
    public async Task Should_ChangeKind_When_CategoryUnused()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var category = await TestData.AddCategoryAsync(factory, userId, name: "Spare");

        using var response = await PutAsync(client, category.Id, Body(name: "Spare", kind: "income"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("kind").GetString().ShouldBe("income");
    }

    [Fact]
    public async Task Should_AllowOwnName_When_Updating()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var category = await TestData.AddCategoryAsync(factory, userId, name: "Spare");

        using var response = await PutAsync(client, category.Id, Body(name: "SPARE", color: "chart-5"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("name").GetString().ShouldBe("SPARE");
    }

    [Fact]
    public async Task Should_ReturnNameTaken_When_RenamingToSibling()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var category = await TestData.AddCategoryAsync(factory, userId, name: "Spare");

        using var response = await PutAsync(client, category.Id, Body(name: " groceries "));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response))["name"].ShouldBe(["validation.categoryNameTaken"]);
    }

    [Fact]
    public async Task Should_Return400_When_BodyInvalidEvenIfCategoryUnknown()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await PutAsync(client, Guid.NewGuid(), Body(name: "   "));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response))["name"].ShouldBe(["validation.required"]);
    }

    [Fact]
    public async Task Should_ReturnSystemCategory_When_DeletingSystemCategory()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        foreach (var (name, kind) in new[] { ("Other", "expense"), ("Transfers", "expense") })
        {
            var category = await FindAsync(client, name, kind);

            using var response = await DeleteAsync(client, category.GetProperty("id").GetGuid());

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ErrorsAsync(response))["id"].ShouldBe(["validation.systemCategory"]);
        }

        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(18);
    }

    [Fact]
    public async Task Should_ReassignTransactionsToOther_When_DeletingUsedCategory()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var category = await TestData.AddCategoryAsync(factory, userId, name: "Used");
        var transaction = await TestData.AddTransactionAsync(factory, account, category, 100);
        var other = await FindAsync(client, "Other", "expense");
        other.GetProperty("inUse").GetBoolean().ShouldBeFalse();
        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(19);

        using var response = await DeleteAsync(client, category.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var categoryId = await db.Transactions.AsNoTracking()
            .Where(stored => stored.Id == transaction.Id)
            .Select(stored => stored.CategoryId)
            .SingleAsync(Ct);
        categoryId.ShouldBe(other.GetProperty("id").GetGuid());
        var page = await ListAsync(client);
        page.GetProperty("total").GetInt32().ShouldBe(18);
        page.GetProperty("items").EnumerateArray()
            .ShouldNotContain(item => item.GetProperty("id").GetGuid() == category.Id);
        (await FindAsync(client, "Other", "expense")).GetProperty("inUse").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_DeleteCategory_When_Unused()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var category = await TestData.AddCategoryAsync(factory, userId, CategoryKind.Income, "Spare");

        using var response = await DeleteAsync(client, category.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(client)).GetProperty("total").GetInt32().ShouldBe(18);
    }

    [Fact]
    public async Task Should_Return404_When_CategoryBelongsToAnotherUser()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var category = await TestData.AddCategoryAsync(factory, await UserIdAsync(owner), name: "Private");

        using var put = await PutAsync(intruder, category.Id, Body(name: "Stolen"));
        using var delete = await DeleteAsync(intruder, category.Id);
        using var unknownPut = await PutAsync(intruder, Guid.NewGuid(), Body(name: "Stolen"));
        using var unknownDelete = await DeleteAsync(intruder, Guid.NewGuid());

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownPut.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknownDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var listed = await ListAsync(owner);
        listed.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == category.Id)
            .GetProperty("name").GetString().ShouldBe("Private");
    }

    private static Dictionary<string, object?> Body(object? name = null, object? kind = null, object? color = null) => new()
    {
        ["name"] = name ?? "Pets",
        ["kind"] = kind ?? "expense",
        ["color"] = color ?? "chart-3"
    };

    private static async Task<JsonElement> FindAsync(AuthClient client, string name, string kind)
    {
        var page = await ListAsync(client);
        return page.GetProperty("items").EnumerateArray().Single(item =>
            item.GetProperty("name").GetString() == name && item.GetProperty("kind").GetString() == kind);
    }

    private static Task<HttpResponseMessage> PutAsync(AuthClient client, Guid id, object body)
    {
        var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, $"{CategoriesPath}/{id}") { Content = JsonContent.Create(body) });
        return client.Http.SendAsync(request, Ct);
    }

    private static Task<HttpResponseMessage> DeleteAsync(AuthClient client, Guid id)
    {
        var request = client.WithXsrf(new HttpRequestMessage(HttpMethod.Delete, $"{CategoriesPath}/{id}"));
        return client.Http.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> ListAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync(CategoriesPath, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
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
