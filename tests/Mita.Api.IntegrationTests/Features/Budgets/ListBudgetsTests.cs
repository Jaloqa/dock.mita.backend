using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Categories;
using Mita.Domain.Periods;
using Mita.Domain.Users;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Budgets;

public sealed class ListBudgetsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string BudgetsPath = "/v1/budgets";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.GetAsync(BudgetsPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_CountExpensesInsidePeriodOnly()
    {
        var today = Today;
        var current = FinancialPeriod.For(today, MonthStart.Day(1));
        var daysLeft = current.DaysLeft(today);
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        var cafes = await TestData.AddCategoryAsync(factory, userId, name: "Coffee shops");
        var salary = await TestData.AddCategoryAsync(factory, userId, CategoryKind.Income, "Paycheck");
        var budget = await TestData.AddBudgetAsync(factory, userId, groceries.Id, current.Key.ToString(), 100_000);
        await TestData.AddTransactionAsync(factory, account, groceries, 30_000, Iso(current.From));
        await TestData.AddTransactionAsync(factory, account, groceries, 20_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, groceries, 99_000, Iso(current.From.AddDays(-1)));
        await TestData.AddTransactionAsync(factory, account, cafes, 5_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, salary, 70_000, Iso(today));

        var page = await GetPageAsync(client, string.Empty);

        page.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["items", "page", "pageSize", "total", "period", "from", "to", "currency", "summary"],
            ignoreOrder: true);
        page.GetProperty("page").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(100);
        page.GetProperty("total").GetInt32().ShouldBe(1);
        page.GetProperty("period").GetString().ShouldBe(current.Key.ToString());
        page.GetProperty("from").GetString().ShouldBe(Iso(current.From));
        page.GetProperty("to").GetString().ShouldBe(Iso(current.To));
        page.GetProperty("currency").GetString().ShouldBe("PLN");
        var summary = page.GetProperty("summary");
        summary.EnumerateObject().Select(property => property.Name).ShouldBe(["planned", "spent", "left", "daysLeft"], ignoreOrder: true);
        summary.GetProperty("planned").GetInt64().ShouldBe(100_000);
        summary.GetProperty("spent").GetInt64().ShouldBe(50_000);
        summary.GetProperty("left").GetInt64().ShouldBe(50_000);
        summary.GetProperty("daysLeft").GetInt32().ShouldBe(daysLeft);
        var item = page.GetProperty("items").EnumerateArray().Single();
        item.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "categoryId", "period", "limit", "spent", "remaining", "status", "perDay"],
            ignoreOrder: true);
        item.GetProperty("id").GetGuid().ShouldBe(budget.Id);
        item.GetProperty("categoryId").GetGuid().ShouldBe(groceries.Id);
        item.GetProperty("period").GetString().ShouldBe(current.Key.ToString());
        item.GetProperty("limit").GetInt64().ShouldBe(100_000);
        item.GetProperty("spent").GetInt64().ShouldBe(50_000);
        item.GetProperty("remaining").GetInt64().ShouldBe(50_000);
        item.GetProperty("status").GetString().ShouldBe("on_track");
        item.GetProperty("perDay").GetInt64().ShouldBe(50_000 / daysLeft);
    }

    [Fact]
    public async Task Should_ReportAlmostSpentAndOverBudget()
    {
        var today = Today;
        var current = FinancialPeriod.For(today, MonthStart.Day(1));
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        var cafes = await TestData.AddCategoryAsync(factory, userId, name: "Coffee shops");
        var first = await TestData.AddBudgetAsync(factory, userId, groceries.Id, current.Key.ToString(), 10_000);
        var second = await TestData.AddBudgetAsync(factory, userId, cafes.Id, current.Key.ToString(), 10_000);
        await TestData.AddTransactionAsync(factory, account, groceries, 9_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, cafes, 12_500, Iso(today));

        var page = await GetPageAsync(client, string.Empty);

        var items = page.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("id").GetGuid()).ShouldBe([first.Id, second.Id]);
        items[0].GetProperty("status").GetString().ShouldBe("almost_spent");
        items[0].GetProperty("remaining").GetInt64().ShouldBe(1_000);
        items[1].GetProperty("status").GetString().ShouldBe("over_budget");
        items[1].GetProperty("remaining").GetInt64().ShouldBe(-2_500);
        items[1].GetProperty("perDay").ValueKind.ShouldBe(JsonValueKind.Null);
        var summary = page.GetProperty("summary");
        summary.GetProperty("planned").GetInt64().ShouldBe(20_000);
        summary.GetProperty("spent").GetInt64().ShouldBe(21_500);
        summary.GetProperty("left").GetInt64().ShouldBe(-1_500);
    }

    [Fact]
    public async Task Should_HaveNoDaysLeft_InPastPeriod()
    {
        var current = FinancialPeriod.For(Today, MonthStart.Day(1));
        var previous = FinancialPeriod.Shift(current.Key, -1, MonthStart.Day(1));
        var next = FinancialPeriod.Shift(current.Key, 1, MonthStart.Day(1));
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        await TestData.AddBudgetAsync(factory, userId, groceries.Id, previous.Key.ToString(), 10_000);
        await TestData.AddBudgetAsync(factory, userId, groceries.Id, next.Key.ToString(), 10_000);

        var past = await GetPageAsync(client, $"period={previous.Key}");
        var future = await GetPageAsync(client, $"period={next.Key}");

        past.GetProperty("summary").GetProperty("daysLeft").GetInt32().ShouldBe(0);
        past.GetProperty("items")[0].GetProperty("perDay").ValueKind.ShouldBe(JsonValueKind.Null);
        future.GetProperty("summary").GetProperty("daysLeft").GetInt32().ShouldBe(next.To.DayNumber - next.From.DayNumber + 1);
        future.GetProperty("items")[0].GetProperty("perDay").GetInt64().ShouldBe(10_000 / (next.To.DayNumber - next.From.DayNumber + 1));
    }

    [Fact]
    public async Task Should_DefaultToCurrentPeriod_And_RejectBadKeys()
    {
        var current = FinancialPeriod.For(Today, MonthStart.Day(1));
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        var page = await GetPageAsync(client, string.Empty);
        using var response = await client.Http.GetAsync($"{BudgetsPath}?period=2026-13", Ct);

        page.GetProperty("period").GetString().ShouldBe(current.Key.ToString());
        page.GetProperty("items").GetArrayLength().ShouldBe(0);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").GetProperty("period").EnumerateArray().Select(error => error.GetString())
            .ShouldBe(["validation.invalidValue"]);
    }

    [Fact]
    public async Task Should_RerangePeriod_When_MonthStartChanges()
    {
        var today = Today;
        var current = FinancialPeriod.For(today, MonthStart.Day(1));
        var shifted = FinancialPeriod.ByKey(current.Key, MonthStart.Day(10));
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        await TestData.AddBudgetAsync(factory, userId, groceries.Id, current.Key.ToString(), 100_000);
        await TestData.AddTransactionAsync(factory, account, groceries, 10_000, Iso(current.From));

        (await GetPageAsync(client, string.Empty)).GetProperty("items")[0].GetProperty("spent").GetInt64().ShouldBe(10_000);

        using (var put = await PutSettingsAsync(client, 10))
        {
            put.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var page = await GetPageAsync(client, $"period={current.Key}");
        page.GetProperty("from").GetString().ShouldBe(Iso(shifted.From));
        page.GetProperty("to").GetString().ShouldBe(Iso(shifted.To));
        page.GetProperty("items")[0].GetProperty("spent").GetInt64().ShouldBe(0);

        await TestData.AddTransactionAsync(factory, account, groceries, 7_000, Iso(shifted.From));

        (await GetPageAsync(client, $"period={current.Key}")).GetProperty("items")[0].GetProperty("spent").GetInt64().ShouldBe(7_000);
        (await GetPageAsync(client, string.Empty)).GetProperty("period").GetString()
            .ShouldBe(FinancialPeriod.For(today, MonthStart.Day(10)).Key.ToString());
    }

    [Fact]
    public async Task Should_IsolateUsers()
    {
        var current = FinancialPeriod.For(Today, MonthStart.Day(1));
        using var owner = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        using var intruder = new AuthClient(factory);
        await intruder.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var groceries = await TestData.AddCategoryAsync(factory, ownerId, name: "Weekly food");
        await TestData.AddBudgetAsync(factory, ownerId, groceries.Id, current.Key.ToString(), 10_000);

        (await GetPageAsync(owner, string.Empty)).GetProperty("total").GetInt32().ShouldBe(1);
        var foreign = await GetPageAsync(intruder, string.Empty);
        foreign.GetProperty("total").GetInt32().ShouldBe(0);
        foreign.GetProperty("summary").GetProperty("planned").GetInt64().ShouldBe(0);
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static async Task<JsonElement> GetPageAsync(AuthClient client, string query)
    {
        using var response = await client.Http.GetAsync($"{BudgetsPath}?{query}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<HttpResponseMessage> PutSettingsAsync(AuthClient client, int monthStartDay)
    {
        var body = new Dictionary<string, object?>
        {
            ["baseCurrency"] = "PLN",
            ["locale"] = "en",
            ["monthStartDay"] = monthStartDay,
            ["theme"] = "system",
            ["notifications"] = new { budgetLimit = false, dailyReminder = true, weeklyReport = true, goals = false }
        };
        return client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, "/v1/settings") { Content = JsonContent.Create(body) }),
            Ct);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
