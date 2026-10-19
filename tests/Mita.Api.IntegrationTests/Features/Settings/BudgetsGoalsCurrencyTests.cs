using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Domain.Users;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Settings;

public sealed class BudgetsGoalsCurrencyTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_ConvertBudgetsAndGoals_When_BaseCurrencyChanges()
    {
        var today = Today;
        var current = FinancialPeriod.For(today, MonthStart.Day(1));
        var contributionDate = today.AddDays(-20);
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(
            isolated,
            new[] { today, current.From, contributionDate }.Distinct()
                .SelectMany(date => new[] { (Currency.EUR, Iso(date), 4m), (Currency.USD, Iso(date), 5m) })
                .ToArray());
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(isolated, userId);
        var groceries = await TestData.AddCategoryAsync(isolated, userId, name: "Weekly food");
        await TestData.AddBudgetAsync(isolated, userId, groceries.Id, current.Key.ToString(), 100_000);
        var transaction = await TestData.AddTransactionAsync(isolated, account, groceries, 40_000, Iso(today));
        var goal = await TestData.AddGoalAsync(isolated, userId, "Holidays", 800_000, 300_000);
        await TestData.AddContributionAsync(isolated, goal, Iso(contributionDate), 300_000);

        (await PutSettingsAsync(client, "EUR")).ShouldBe(HttpStatusCode.OK);

        var budgets = await GetAsync(client, "/v1/budgets");
        budgets.GetProperty("currency").GetString().ShouldBe("EUR");
        var budget = budgets.GetProperty("items")[0];
        budget.GetProperty("limit").GetInt64().ShouldBe(25_000);
        budget.GetProperty("spent").GetInt64().ShouldBe(await BaseAmountAsync(client, transaction.Id));
        budget.GetProperty("spent").GetInt64().ShouldBe(10_000);
        var goals = await GetAsync(client, "/v1/goals");
        goals.GetProperty("currency").GetString().ShouldBe("EUR");
        var item = goals.GetProperty("items")[0];
        item.GetProperty("target").GetInt64().ShouldBe(200_000);
        item.GetProperty("saved").GetInt64().ShouldBe(75_000);
        goals.GetProperty("summary").GetProperty("monthly").GetInt64().ShouldBe(25_000);

        (await PutSettingsAsync(client, "USD")).ShouldBe(HttpStatusCode.OK);

        (await GetAsync(client, "/v1/budgets")).GetProperty("items")[0].GetProperty("limit").GetInt64().ShouldBe(20_000);
        (await GetAsync(client, "/v1/goals")).GetProperty("items")[0].GetProperty("target").GetInt64().ShouldBe(160_000);

        (await PutSettingsAsync(client, "PLN")).ShouldBe(HttpStatusCode.OK);

        var restoredBudget = (await GetAsync(client, "/v1/budgets")).GetProperty("items")[0];
        restoredBudget.GetProperty("limit").GetInt64().ShouldBe(100_000);
        restoredBudget.GetProperty("spent").GetInt64().ShouldBe(40_000);
        var restoredGoal = (await GetAsync(client, "/v1/goals")).GetProperty("items")[0];
        restoredGoal.GetProperty("target").GetInt64().ShouldBe(800_000);
        restoredGoal.GetProperty("saved").GetInt64().ShouldBe(300_000);
    }

    [Fact]
    public async Task Should_Return503_When_BudgetCurrencyHasNoRate()
    {
        var current = FinancialPeriod.For(Today, MonthStart.Day(1));
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var groceries = await TestData.AddCategoryAsync(isolated, userId, name: "Weekly food");
        await TestData.AddBudgetAsync(isolated, userId, groceries.Id, current.Key.ToString(), 100_000);

        await AssertRefusedAsync(client);
    }

    [Fact]
    public async Task Should_Return503_When_GoalCurrencyHasNoRate()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        await TestData.AddGoalAsync(isolated, userId, "Holidays", 800_000);

        await AssertRefusedAsync(client);
    }

    private static async Task AssertRefusedAsync(AuthClient client)
    {
        using var response = await SendSettingsAsync(client, "EUR");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString().ShouldBe("rates.unavailable");
        (await GetAsync(client, "/v1/settings")).GetProperty("baseCurrency").GetString().ShouldBe("PLN");
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> GetAsync(AuthClient client, string path)
    {
        using var response = await client.Http.GetAsync(path, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<long> BaseAmountAsync(AuthClient client, Guid transactionId) =>
        (await GetAsync(client, $"/v1/transactions/{transactionId}")).GetProperty("baseAmount").GetInt64();

    private static async Task<Guid> UserIdAsync(AuthClient client) =>
        (await GetAsync(client, "/v1/me")).GetProperty("id").GetGuid();

    private static async Task<HttpStatusCode> PutSettingsAsync(AuthClient client, string baseCurrency)
    {
        using var response = await SendSettingsAsync(client, baseCurrency);
        return response.StatusCode;
    }

    private static Task<HttpResponseMessage> SendSettingsAsync(AuthClient client, string baseCurrency)
    {
        var body = new Dictionary<string, object?>
        {
            ["baseCurrency"] = baseCurrency,
            ["locale"] = "en",
            ["monthStartDay"] = 1,
            ["theme"] = "system",
            ["notifications"] = new { budgetLimit = false, dailyReminder = true, weeklyReport = true, goals = false }
        };
        return client.Http.SendAsync(
            client.WithXsrf(new HttpRequestMessage(HttpMethod.Put, "/v1/settings") { Content = JsonContent.Create(body) }),
            Ct);
    }
}
