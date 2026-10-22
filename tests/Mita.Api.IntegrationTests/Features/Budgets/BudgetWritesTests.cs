using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Categories;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Budgets;

public sealed class BudgetWritesTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string BudgetsPath = "/v1/budgets";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static PeriodKey CurrentKey => FinancialPeriod.For(Today, MonthStart.Day(1)).Key;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.PostAsJsonAsync(BudgetsPath, new { }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_CreateBudget_When_InputValid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");

        using var response = await SendAsync(client, HttpMethod.Post, BudgetsPath, Body(groceries.Id, CurrentKey.ToString(), 100_000));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "categoryId", "period", "limit", "spent", "remaining", "status", "perDay"],
            ignoreOrder: true);
        response.Headers.Location!.ToString().ShouldBe($"{BudgetsPath}/{body.GetProperty("id").GetGuid()}");
        body.GetProperty("categoryId").GetGuid().ShouldBe(groceries.Id);
        body.GetProperty("period").GetString().ShouldBe(CurrentKey.ToString());
        body.GetProperty("limit").GetInt64().ShouldBe(100_000);
        body.GetProperty("spent").GetInt64().ShouldBe(0);
        body.GetProperty("remaining").GetInt64().ShouldBe(100_000);
        body.GetProperty("status").GetString().ShouldBe("on_track");
    }

    [Fact]
    public async Task Should_ReturnFieldErrors_When_CategoryInvalid()
    {
        using var owner = new AuthClient(factory);
        using var other = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await other.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var income = await TestData.AddCategoryAsync(factory, ownerId, CategoryKind.Income, "Paycheck");
        var foreign = await TestData.AddCategoryAsync(factory, await UserIdAsync(other), name: "Foreign food");
        var transfers = await SystemCategoryIdAsync(owner, "transfer");
        var period = CurrentKey.ToString();

        using var incomeResponse = await SendAsync(owner, HttpMethod.Post, BudgetsPath, Body(income.Id, period, 100));
        using var transfersResponse = await SendAsync(owner, HttpMethod.Post, BudgetsPath, Body(transfers, period, 100));
        using var foreignResponse = await SendAsync(owner, HttpMethod.Post, BudgetsPath, Body(foreign.Id, period, 100));
        using var unknownResponse = await SendAsync(owner, HttpMethod.Post, BudgetsPath, Body(Guid.NewGuid(), period, 100));
        using var textResponse = await SendAsync(owner, HttpMethod.Post, BudgetsPath, new { categoryId = "cat_x", period, limit = 100 });

        await ShouldHaveCategoryErrorAsync(incomeResponse, "validation.expenseCategoryOnly");
        await ShouldHaveCategoryErrorAsync(transfersResponse, "validation.unknownCategory");
        await ShouldHaveCategoryErrorAsync(foreignResponse, "validation.unknownCategory");
        await ShouldHaveCategoryErrorAsync(unknownResponse, "validation.unknownCategory");
        await ShouldHaveCategoryErrorAsync(textResponse, "validation.unknownCategory");
    }

    [Fact]
    public async Task Should_NotReportBudgetExists_When_PeriodInvalid()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        await TestData.AddBudgetAsync(factory, userId, groceries.Id, CurrentKey.ToString(), 100);

        using var response = await SendAsync(client, HttpMethod.Post, BudgetsPath, Body(groceries.Id, "2026-13", 100));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(["period"]);
    }

    [Fact]
    public async Task Should_AllowOneBudgetPerCategoryAndPeriod()
    {
        var current = CurrentKey;
        var previous = FinancialPeriod.Shift(current, -1, MonthStart.Day(1));
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        var cafes = await TestData.AddCategoryAsync(factory, userId, name: "Coffee shops");
        var groceriesBudget = await TestData.AddBudgetAsync(factory, userId, groceries.Id, current.ToString(), 10_000);
        var cafesBudget = await TestData.AddBudgetAsync(factory, userId, cafes.Id, current.ToString(), 5_000);

        using var duplicate = await SendAsync(client, HttpMethod.Post, BudgetsPath, Body(groceries.Id, current.ToString(), 1));
        using var earlier = await SendAsync(client, HttpMethod.Post, BudgetsPath, Body(groceries.Id, previous.Key.ToString(), 1));
        using var taken = await SendAsync(
            client, HttpMethod.Put, $"{BudgetsPath}/{cafesBudget.Id}", Body(groceries.Id, current.ToString(), 5_000));
        using var own = await SendAsync(
            client, HttpMethod.Put, $"{BudgetsPath}/{groceriesBudget.Id}", Body(groceries.Id, current.ToString(), 12_345));

        await ShouldHaveCategoryErrorAsync(duplicate, "validation.budgetExists");
        earlier.StatusCode.ShouldBe(HttpStatusCode.Created);
        await ShouldHaveCategoryErrorAsync(taken, "validation.budgetExists");
        own.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await own.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("limit").GetInt64().ShouldBe(12_345);
    }

    [Fact]
    public async Task Should_StoreBaseCurrency_When_Written()
    {
        var from = FinancialPeriod.ByKey(CurrentKey, MonthStart.Day(1)).From;
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, from.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), 4.25m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var groceries = await TestData.AddCategoryAsync(isolated, userId, name: "Weekly food");
        var budget = await TestData.AddBudgetAsync(isolated, userId, groceries.Id, CurrentKey.ToString(), 10_000);

        using (var toEuro = await PutSettingsAsync(client, "EUR"))
        {
            toEuro.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var update = await SendAsync(
            client, HttpMethod.Put, $"{BudgetsPath}/{budget.Id}", Body(groceries.Id, CurrentKey.ToString(), 30_000));

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await update.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("limit").GetInt64().ShouldBe(30_000);

        using (var toZloty = await PutSettingsAsync(client, "PLN"))
        {
            toZloty.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var list = await client.Http.GetAsync($"{BudgetsPath}?period={CurrentKey}", Ct);
        var item = (await list.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items")[0];
        item.GetProperty("limit").GetInt64().ShouldBe(MinorUnits.Convert(30_000, 4.25m, Currency.EUR, Currency.PLN));
    }

    [Fact]
    public async Task Should_Return404_When_BudgetBelongsToAnotherUser()
    {
        using var owner = new AuthClient(factory);
        using var intruder = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        await intruder.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var intruderId = await UserIdAsync(intruder);
        var groceries = await TestData.AddCategoryAsync(factory, ownerId, name: "Weekly food");
        var mine = await TestData.AddCategoryAsync(factory, intruderId, name: "My food");
        var budget = await TestData.AddBudgetAsync(factory, ownerId, groceries.Id, CurrentKey.ToString(), 10_000);

        using var put = await SendAsync(
            intruder, HttpMethod.Put, $"{BudgetsPath}/{budget.Id}", Body(mine.Id, CurrentKey.ToString(), 1));
        using var delete = await SendAsync(intruder, HttpMethod.Delete, $"{BudgetsPath}/{budget.Id}", null);

        put.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        delete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var list = await owner.Http.GetAsync($"{BudgetsPath}?period={CurrentKey}", Ct);
        (await list.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("total").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Return400_When_BodyInvalidOnUnknownId()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await SendAsync(
            client, HttpMethod.Put, $"{BudgetsPath}/{Guid.NewGuid()}", new { categoryId = Guid.NewGuid(), period = "2026-13", limit = 0 });
        using var valid = await SendAsync(
            client, HttpMethod.Put, $"{BudgetsPath}/{Guid.NewGuid()}", Body(Guid.NewGuid(), CurrentKey.ToString(), 1));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        valid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Should_DeleteBudget()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        var budget = await TestData.AddBudgetAsync(factory, userId, groceries.Id, CurrentKey.ToString(), 10_000);

        using var first = await SendAsync(client, HttpMethod.Delete, $"{BudgetsPath}/{budget.Id}", null);
        using var second = await SendAsync(client, HttpMethod.Delete, $"{BudgetsPath}/{budget.Id}", null);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_NotFail_When_BudgetCreatedWhileCategoryDeleted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var period = CurrentKey.ToString();

        for (var round = 0; round < 20; round++)
        {
            var category = await TestData.AddCategoryAsync(factory, userId);

            var delete = SendAsync(client, HttpMethod.Delete, $"/v1/categories/{category.Id}", null);
            var create = SendAsync(client, HttpMethod.Post, BudgetsPath, Body(category.Id, period, 1_000));
            await Task.WhenAll(delete, create);
            using var deleteResponse = await delete;
            using var createResponse = await create;

            deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            if (createResponse.StatusCode == HttpStatusCode.Created)
            {
                continue;
            }

            createResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            await ShouldHaveCategoryErrorAsync(createResponse, "validation.unknownCategory");
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orphans = db.Budgets.Count(budget => budget.UserId == userId && !db.Categories.Any(category => category.Id == budget.CategoryId));
        orphans.ShouldBe(0);
    }

    [Fact]
    public async Task Should_NotFail_When_BudgetDeletedWhileCategoryDeleted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var period = CurrentKey.ToString();

        for (var round = 0; round < 20; round++)
        {
            var category = await TestData.AddCategoryAsync(factory, userId);
            var budget = await TestData.AddBudgetAsync(factory, userId, category.Id, period, 1_000);

            var deleteCategory = SendAsync(client, HttpMethod.Delete, $"/v1/categories/{category.Id}", null);
            var deleteBudget = SendAsync(client, HttpMethod.Delete, $"{BudgetsPath}/{budget.Id}", null);
            await Task.WhenAll(deleteCategory, deleteBudget);
            using var categoryResponse = await deleteCategory;
            using var budgetResponse = await deleteBudget;

            categoryResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            budgetResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
    }

    [Fact]
    public async Task Should_KeepMovedCategory_When_BudgetUpdatedWhileCategoryDeleted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var period = CurrentKey.ToString();

        for (var round = 0; round < 20; round++)
        {
            var category = await TestData.AddCategoryAsync(factory, userId);
            var target = await TestData.AddCategoryAsync(factory, userId);
            var budget = await TestData.AddBudgetAsync(factory, userId, category.Id, period, 1_000);

            var deleteCategory = SendAsync(client, HttpMethod.Delete, $"/v1/categories/{category.Id}", null);
            var update = SendAsync(client, HttpMethod.Put, $"{BudgetsPath}/{budget.Id}", Body(target.Id, period, 2_000));
            await Task.WhenAll(deleteCategory, update);
            using var categoryResponse = await deleteCategory;
            using var updateResponse = await update;

            categoryResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Budgets.Single(stored => stored.Id == budget.Id).CategoryId.ShouldBe(target.Id);
        }
    }

    private static object Body(Guid categoryId, string period, long limit) => new { categoryId, period, limit };

    private static Task<HttpResponseMessage> SendAsync(AuthClient client, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.Http.SendAsync(client.WithXsrf(request), Ct);
    }

    private static Task<HttpResponseMessage> PutSettingsAsync(AuthClient client, string baseCurrency) =>
        SendAsync(
            client,
            HttpMethod.Put,
            "/v1/settings",
            new Dictionary<string, object?>
            {
                ["baseCurrency"] = baseCurrency,
                ["locale"] = "en",
                ["monthStartDay"] = 1,
                ["theme"] = "system",
                ["notifications"] = new { budgetLimit = false, dailyReminder = true, weeklyReport = true, goals = false }
            });

    private static async Task ShouldHaveCategoryErrorAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(["categoryId"]);
        errors.GetProperty("categoryId").EnumerateArray().Select(error => error.GetString()).ShouldBe([code]);
    }

    private static async Task<Guid> SystemCategoryIdAsync(AuthClient client, string system)
    {
        using var response = await client.Http.GetAsync("/v1/categories?pageSize=100", Ct);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray()
            .First(item => item.TryGetProperty("system", out var value) && value.ValueKind == JsonValueKind.String && value.GetString() == system)
            .GetProperty("id").GetGuid();
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
