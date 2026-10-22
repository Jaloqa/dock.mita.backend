using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Budgets;

public sealed class CopyBudgetsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string CopyPath = "/v1/budgets/copy";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PeriodKey CurrentKey => FinancialPeriod.For(DateOnly.FromDateTime(DateTime.UtcNow), MonthStart.Day(1)).Key;

    private static PeriodKey PreviousKey => FinancialPeriod.Shift(CurrentKey, -1, MonthStart.Day(1)).Key;

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.PostAsJsonAsync(CopyPath, new { period = CurrentKey.ToString() }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_CopyOnlyMissingCategories()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        var cafes = await TestData.AddCategoryAsync(factory, userId, name: "Coffee shops");
        await TestData.AddBudgetAsync(factory, userId, groceries.Id, PreviousKey.ToString(), 10_000);
        await TestData.AddBudgetAsync(factory, userId, cafes.Id, PreviousKey.ToString(), 5_000);
        await TestData.AddBudgetAsync(factory, userId, cafes.Id, CurrentKey.ToString(), 7_000);

        using var response = await CopyAsync(client, CurrentKey.ToString());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        body.EnumerateObject().Select(property => property.Name).ShouldBe(["copied"]);
        body.GetProperty("copied").GetInt32().ShouldBe(1);
        var items = await ListAsync(client, CurrentKey.ToString());
        items.Select(item => (item.GetProperty("categoryId").GetGuid(), item.GetProperty("limit").GetInt64()))
            .ShouldBe([(cafes.Id, 7_000L), (groceries.Id, 10_000L)]);

        using var second = await CopyAsync(client, CurrentKey.ToString());
        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await second.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(["period"]);
        errors.GetProperty("period").EnumerateArray().Select(error => error.GetString()).ShouldBe(["validation.nothingToCopy"]);

        using var invalid = await CopyAsync(client, "x");
        using var missing = await SendAsync(client, new { });
        await ShouldHavePeriodErrorAsync(invalid, "validation.invalidValue");
        await ShouldHavePeriodErrorAsync(missing, "validation.required");
    }

    [Fact]
    public async Task Should_KeepSourceOrder_When_Copying()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var created = new List<Guid>();
        foreach (var name in new[] { "Alpha", "Bravo", "Charlie" })
        {
            var category = await TestData.AddCategoryAsync(factory, userId, name: name);
            await TestData.AddBudgetAsync(factory, userId, category.Id, PreviousKey.ToString(), 1_000);
            created.Add(category.Id);
        }

        using var response = await CopyAsync(client, CurrentKey.ToString());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await ListAsync(client, CurrentKey.ToString());
        items.Select(item => item.GetProperty("categoryId").GetGuid()).ShouldBe(created);
    }

    [Fact]
    public async Task Should_ConvertLimit_When_SourceInOtherCurrency()
    {
        var sourceFrom = FinancialPeriod.ByKey(PreviousKey, MonthStart.Day(1)).From;
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(
            isolated, (Currency.EUR, sourceFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), 4.25m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var groceries = await TestData.AddCategoryAsync(isolated, userId, name: "Weekly food");
        await TestData.AddBudgetAsync(isolated, userId, groceries.Id, PreviousKey.ToString(), 10_001, Currency.EUR);

        using var response = await CopyAsync(client, CurrentKey.ToString());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await ListAsync(client, CurrentKey.ToString());
        items.Single().GetProperty("limit").GetInt64().ShouldBe(MinorUnits.Convert(10_001, 4.25m, Currency.EUR, Currency.PLN));
    }

    [Fact]
    public async Task Should_CopyOneMinorUnit_When_ConvertedLimitRoundsToZero()
    {
        var sourceFrom = FinancialPeriod.ByKey(PreviousKey, MonthStart.Day(1)).From;
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(
            isolated, (Currency.UAH, sourceFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), 0.1m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var groceries = await TestData.AddCategoryAsync(isolated, userId, name: "Weekly food");
        await TestData.AddBudgetAsync(isolated, userId, groceries.Id, PreviousKey.ToString(), 1, Currency.UAH);

        using var response = await CopyAsync(client, CurrentKey.ToString());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = await ListAsync(client, CurrentKey.ToString());
        items.Single().GetProperty("limit").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_NotDuplicate_When_CopyRacesCreate()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var target = CurrentKey.ToString();
        var targetKey = CurrentKey;

        for (var round = 0; round < 10; round++)
        {
            var category = await TestData.AddCategoryAsync(factory, userId);
            await TestData.AddBudgetAsync(factory, userId, category.Id, PreviousKey.ToString(), 1_000);

            var copy = CopyAsync(client, target);
            var create = SendAsync(client, "/v1/budgets", new { categoryId = category.Id, period = target, limit = 2_000 });
            await Task.WhenAll(copy, create);
            using var copyResponse = await copy;
            using var createResponse = await create;

            ((int)copyResponse.StatusCode).ShouldBeOneOf(200, 400);
            ((int)createResponse.StatusCode).ShouldBeOneOf(201, 400);

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Budgets.Count(budget => budget.UserId == userId && budget.Period == targetKey && budget.CategoryId == category.Id)
                .ShouldBe(1);
        }
    }

    [Fact]
    public async Task Should_NotFail_When_CopyRacesCategoryDelete()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var source = PreviousKey.ToString();
        var target = CurrentKey.ToString();
        var otherId = await ExpenseOtherIdAsync(client);
        await TestData.AddBudgetAsync(factory, userId, otherId, source, 1_000);
        for (var filler = 0; filler < 40; filler++)
        {
            var category = await TestData.AddCategoryAsync(factory, userId);
            await TestData.AddBudgetAsync(factory, userId, category.Id, source, 1_000);
        }

        for (var round = 0; round < 30; round++)
        {
            var category = await TestData.AddCategoryAsync(factory, userId);
            await TestData.AddBudgetAsync(factory, userId, category.Id, source, 1_000);

            var copy = CopyAsync(client, target);
            var delete = client.Http.SendAsync(
                client.WithXsrf(new HttpRequestMessage(HttpMethod.Delete, $"/v1/categories/{category.Id}")), Ct);
            await Task.WhenAll(copy, delete);
            using var copyResponse = await copy;
            using var deleteResponse = await delete;

            deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            ((int)copyResponse.StatusCode).ShouldBeOneOf(200, 400);
        }
    }

    private static async Task<Guid> ExpenseOtherIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/categories?kind=expense", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "Other")
            .GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> CopyAsync(AuthClient client, string period) =>
        SendAsync(client, new { period });

    private static Task<HttpResponseMessage> SendAsync(AuthClient client, object body) => SendAsync(client, CopyPath, body);

    private static Task<HttpResponseMessage> SendAsync(AuthClient client, string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        return client.Http.SendAsync(client.WithXsrf(request), Ct);
    }

    private static async Task<List<JsonElement>> ListAsync(AuthClient client, string period)
    {
        using var response = await client.Http.GetAsync($"/v1/budgets?period={period}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray().ToList();
    }

    private static async Task ShouldHavePeriodErrorAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        errors.EnumerateObject().Select(property => property.Name).ShouldBe(["period"]);
        errors.GetProperty("period").EnumerateArray().Select(error => error.GetString()).ShouldBe([code]);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
