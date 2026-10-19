using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Categories;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Reports;

public sealed class ReportsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string ReportsPath = "/v1/reports";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.GetAsync(ReportsPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_SumPerPeriod_OverSixMonthsByDefault()
    {
        var today = Today;
        var current = FinancialPeriod.For(today, MonthStart.Day(1));
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var housing = await TestData.AddCategoryAsync(factory, userId, name: "Housing bills");
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        var salary = await TestData.AddCategoryAsync(factory, userId, CategoryKind.Income, "Paycheck");
        await TestData.AddTransactionAsync(factory, account, salary, 700_000, Iso(current.From));
        await TestData.AddTransactionAsync(factory, account, housing, 80_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, salary, 500_000, Iso(Back(current, 5).From));
        await TestData.AddTransactionAsync(factory, account, groceries, 40_000, Iso(Back(current, 5).To));
        await TestData.AddTransactionAsync(factory, account, groceries, 10_001, Iso(Back(current, 2).From));
        await TestData.AddTransactionAsync(factory, account, groceries, 99_999, Iso(Back(current, 6).To));

        var report = await GetReportAsync(client, string.Empty);

        report.EnumerateObject().Select(property => property.Name).ShouldBe(
            [
                "range", "from", "to", "currency", "periods", "totals", "averageIncome", "averageExpense",
                "savingsRate", "structure", "hasTransactions"
            ],
            ignoreOrder: true);
        report.GetProperty("range").GetString().ShouldBe("6m");
        report.GetProperty("from").GetString().ShouldBe(Iso(Back(current, 5).From));
        report.GetProperty("to").GetString().ShouldBe(Iso(current.To));
        report.GetProperty("currency").GetString().ShouldBe("PLN");
        var periods = report.GetProperty("periods").EnumerateArray().ToList();
        periods.Select(period => period.GetProperty("period").GetString()).ShouldBe(
            Enumerable.Range(0, 6).Select(index => Back(current, 5 - index).Key.ToString()).ToList());
        periods[0].EnumerateObject().Select(property => property.Name).ShouldBe(
            ["period", "from", "to", "income", "expense"],
            ignoreOrder: true);
        periods[0].GetProperty("from").GetString().ShouldBe(Iso(Back(current, 5).From));
        periods[0].GetProperty("to").GetString().ShouldBe(Iso(Back(current, 5).To));
        periods[0].GetProperty("income").GetInt64().ShouldBe(500_000);
        periods[0].GetProperty("expense").GetInt64().ShouldBe(40_000);
        periods[3].GetProperty("income").GetInt64().ShouldBe(0);
        periods[3].GetProperty("expense").GetInt64().ShouldBe(10_001);
        periods[5].GetProperty("income").GetInt64().ShouldBe(700_000);
        periods[5].GetProperty("expense").GetInt64().ShouldBe(80_000);
        var totals = report.GetProperty("totals");
        totals.EnumerateObject().Select(property => property.Name).ShouldBe(["income", "expense"], ignoreOrder: true);
        totals.GetProperty("income").GetInt64().ShouldBe(1_200_000);
        totals.GetProperty("expense").GetInt64().ShouldBe(130_001);
        report.GetProperty("averageIncome").GetInt64().ShouldBe(200_000);
        report.GetProperty("averageExpense").GetInt64().ShouldBe(21_667);
        report.GetProperty("savingsRate").GetDouble().ShouldBe(89.2);
        report.GetProperty("hasTransactions").GetBoolean().ShouldBeTrue();
        var structure = report.GetProperty("structure");
        structure.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["period", "from", "to", "total", "items"],
            ignoreOrder: true);
        structure.GetProperty("items")[0].EnumerateObject().Select(property => property.Name).ShouldBe(
            ["categoryId", "name", "color", "amount", "percent"],
            ignoreOrder: true);
    }

    [Fact]
    public async Task Should_CoverThreeAndTwelvePeriods()
    {
        var current = FinancialPeriod.For(Today, MonthStart.Day(1));
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        await TestData.AddTransactionAsync(factory, account, groceries, 1_000, Iso(Back(current, 2).From));
        await TestData.AddTransactionAsync(factory, account, groceries, 2_000, Iso(Back(current, 3).To));
        await TestData.AddTransactionAsync(factory, account, groceries, 4_000, Iso(Back(current, 11).From));
        await TestData.AddTransactionAsync(factory, account, groceries, 8_000, Iso(Back(current, 12).To));

        var three = await GetReportAsync(client, "range=3m");
        var twelve = await GetReportAsync(client, "range=12m");

        three.GetProperty("range").GetString().ShouldBe("3m");
        three.GetProperty("from").GetString().ShouldBe(Iso(Back(current, 2).From));
        three.GetProperty("periods").GetArrayLength().ShouldBe(3);
        three.GetProperty("totals").GetProperty("income").GetInt64().ShouldBe(0);
        three.GetProperty("totals").GetProperty("expense").GetInt64().ShouldBe(1_000);
        three.GetProperty("averageIncome").GetInt64().ShouldBe(0);
        three.GetProperty("averageExpense").GetInt64().ShouldBe(333);
        three.GetProperty("savingsRate").ValueKind.ShouldBe(JsonValueKind.Null);
        twelve.GetProperty("range").GetString().ShouldBe("12m");
        twelve.GetProperty("from").GetString().ShouldBe(Iso(Back(current, 11).From));
        twelve.GetProperty("periods").GetArrayLength().ShouldBe(12);
        twelve.GetProperty("totals").GetProperty("expense").GetInt64().ShouldBe(7_000);
        twelve.GetProperty("averageExpense").GetInt64().ShouldBe(583);
    }

    [Fact]
    public async Task Should_BreakDownCurrentPeriod_LargestFirst()
    {
        var today = Today;
        var current = FinancialPeriod.For(today, MonthStart.Day(1));
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var rent = await TestData.AddCategoryAsync(factory, userId, name: "Monthly rent");
        var food = await TestData.AddCategoryAsync(factory, userId, name: "Meals out");
        var shops = await TestData.AddCategoryAsync(factory, userId, name: "Shops mall");
        var pharmacy = await TestData.AddCategoryAsync(factory, userId, name: "Pharmacy run");
        var coffee = await TestData.AddCategoryAsync(factory, userId, name: "Coffee shops");
        var salary = await TestData.AddCategoryAsync(factory, userId, CategoryKind.Income, "Paycheck");
        var other = await SystemOtherExpenseAsync(userId);
        await TestData.AddTransactionAsync(factory, account, rent, 210_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, food, 124_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, other, 50_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, shops, 20_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, pharmacy, 20_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, coffee, 7_000, Iso(Back(current, 1).From));
        await TestData.AddTransactionAsync(factory, account, salary, 500_000, Iso(today));

        var report = await GetReportAsync(client, string.Empty);

        var structure = report.GetProperty("structure");
        structure.GetProperty("period").GetString().ShouldBe(current.Key.ToString());
        structure.GetProperty("from").GetString().ShouldBe(Iso(current.From));
        structure.GetProperty("to").GetString().ShouldBe(Iso(current.To));
        structure.GetProperty("total").GetInt64().ShouldBe(424_000);
        var items = structure.GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("categoryId").GetGuid()).ShouldBe(
            [rent.Id, food.Id, other.Id, pharmacy.Id, shops.Id]);
        items.Select(item => item.GetProperty("amount").GetInt64()).ShouldBe([210_000, 124_000, 50_000, 20_000, 20_000]);
        items.Select(item => item.GetProperty("percent").GetDouble()).ShouldBe([49.5, 29.3, 11.8, 4.7, 4.7]);
        items.Select(item => item.GetProperty("percent").GetDouble() * 10).Sum().ShouldBe(1000, 0.0001);
        items[0].GetProperty("name").GetString().ShouldBe("Monthly rent");
        items[0].GetProperty("color").GetString().ShouldBe("chart-1");
        items[2].GetProperty("color").GetString().ShouldBe(WireColor(other.Color));
    }

    [Fact]
    public async Task Should_LeaveTransfersOut()
    {
        var today = Today;
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var first = await TestData.AddAccountAsync(factory, userId, openingBalance: 100_000);
        var second = await TestData.AddAccountAsync(factory, userId);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        await TestData.AddTransactionAsync(factory, first, groceries, 10_000, Iso(today));
        await TestData.AddTransferAsync(factory, first, second, 25_000);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var transfer = await db.Categories.SingleAsync(
                category => category.UserId == userId && category.System == SystemCategory.Transfer,
                Ct);
            await TestData.AddTransactionAsync(factory, first, transfer, 25_000, Iso(today));
        }

        var report = await GetReportAsync(client, string.Empty);

        report.GetProperty("totals").GetProperty("income").GetInt64().ShouldBe(0);
        report.GetProperty("totals").GetProperty("expense").GetInt64().ShouldBe(10_000);
        var items = report.GetProperty("structure").GetProperty("items").EnumerateArray().ToList();
        items.Select(item => item.GetProperty("categoryId").GetGuid()).ShouldBe([groceries.Id]);
    }

    [Fact]
    public async Task Should_FollowMonthStart()
    {
        var today = Today;
        var shifted = MonthStart.Day(10);
        var period = FinancialPeriod.For(today, shifted);
        var before = FinancialPeriod.Shift(period.Key, -1, shifted);
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        await TestData.AddTransactionAsync(factory, account, groceries, 30_000, Iso(period.From));
        await TestData.AddTransactionAsync(factory, account, groceries, 20_000, Iso(period.From.AddDays(-1)));
        using (var put = await PutSettingsAsync(client, "PLN", 10))
        {
            put.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var report = await GetReportAsync(client, string.Empty);

        var periods = report.GetProperty("periods").EnumerateArray().ToList();
        periods[5].GetProperty("period").GetString().ShouldBe(period.Key.ToString());
        periods[5].GetProperty("from").GetString().ShouldBe(Iso(period.From));
        periods[5].GetProperty("to").GetString().ShouldBe(Iso(period.To));
        periods[5].GetProperty("income").GetInt64().ShouldBe(0);
        periods[5].GetProperty("expense").GetInt64().ShouldBe(30_000);
        periods[4].GetProperty("period").GetString().ShouldBe(before.Key.ToString());
        periods[4].GetProperty("to").GetString().ShouldBe(Iso(period.From.AddDays(-1)));
        periods[4].GetProperty("expense").GetInt64().ShouldBe(20_000);
        report.GetProperty("totals").GetProperty("expense").GetInt64().ShouldBe(50_000);
        var structure = report.GetProperty("structure");
        structure.GetProperty("period").GetString().ShouldBe(period.Key.ToString());
        structure.GetProperty("total").GetInt64().ShouldBe(30_000);
    }

    [Fact]
    public async Task Should_RepriceReport_When_BaseCurrencyChanges()
    {
        var today = Today;
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, Iso(today), 4m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(isolated, userId);
        var groceries = await TestData.AddCategoryAsync(isolated, userId, name: "Weekly food");
        var salary = await TestData.AddCategoryAsync(isolated, userId, CategoryKind.Income, "Paycheck");
        await TestData.AddTransactionAsync(isolated, account, groceries, 10_000, Iso(today));
        await TestData.AddTransactionAsync(isolated, account, salary, 50_000, Iso(today));

        using (var put = await PutSettingsAsync(client, "EUR", 1))
        {
            put.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var report = await GetReportAsync(client, string.Empty);

        report.GetProperty("currency").GetString().ShouldBe("EUR");
        var last = report.GetProperty("periods").EnumerateArray().Last();
        last.GetProperty("income").GetInt64().ShouldBe(12_500);
        last.GetProperty("expense").GetInt64().ShouldBe(2_500);
        report.GetProperty("structure").GetProperty("items")[0].GetProperty("amount").GetInt64().ShouldBe(2_500);
    }

    [Fact]
    public async Task Should_ReturnEmptyReport_And_IsolateUsers()
    {
        var current = FinancialPeriod.For(Today, MonthStart.Day(1));
        using var owner = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        using var intruder = new AuthClient(factory);
        await intruder.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var account = await TestData.AddAccountAsync(factory, ownerId);
        var groceries = await TestData.AddCategoryAsync(factory, ownerId, name: "Weekly food");
        await TestData.AddTransactionAsync(factory, account, groceries, 10_000, Iso(Today));

        var report = await GetReportAsync(intruder, string.Empty);

        report.GetProperty("totals").GetProperty("income").GetInt64().ShouldBe(0);
        report.GetProperty("totals").GetProperty("expense").GetInt64().ShouldBe(0);
        report.GetProperty("averageIncome").GetInt64().ShouldBe(0);
        report.GetProperty("averageExpense").GetInt64().ShouldBe(0);
        report.GetProperty("savingsRate").ValueKind.ShouldBe(JsonValueKind.Null);
        report.GetProperty("hasTransactions").GetBoolean().ShouldBeFalse();
        var structure = report.GetProperty("structure");
        structure.GetProperty("period").GetString().ShouldBe(current.Key.ToString());
        structure.GetProperty("total").GetInt64().ShouldBe(0);
        structure.GetProperty("items").GetArrayLength().ShouldBe(0);
        var periods = report.GetProperty("periods").EnumerateArray().ToList();
        periods.Count.ShouldBe(6);
        periods.ShouldAllBe(period => period.GetProperty("income").GetInt64() == 0 && period.GetProperty("expense").GetInt64() == 0);
        (await GetReportAsync(owner, string.Empty)).GetProperty("hasTransactions").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData("range=1m")]
    [InlineData("range=")]
    public async Task Should_RejectUnknownRange(string query)
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await client.Http.GetAsync($"{ReportsPath}?{query}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").GetProperty("range").EnumerateArray().Select(error => error.GetString())
            .ShouldBe(["validation.invalidValue"]);
    }

    private static FinancialPeriod Back(FinancialPeriod current, int offset) =>
        FinancialPeriod.Shift(current.Key, -offset, MonthStart.Day(1));

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string WireColor(CategoryColor color) => color switch
    {
        CategoryColor.Chart1 => "chart-1",
        CategoryColor.Chart2 => "chart-2",
        CategoryColor.Chart3 => "chart-3",
        CategoryColor.Chart4 => "chart-4",
        CategoryColor.Chart5 => "chart-5",
        CategoryColor.Chart6 => "chart-6",
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, null)
    };

    private async Task<Category> SystemOtherExpenseAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Categories.AsNoTracking().SingleAsync(
            category => category.UserId == userId
                && category.System == SystemCategory.Other
                && category.Kind == CategoryKind.Expense,
            Ct);
    }

    private static async Task<JsonElement> GetReportAsync(AuthClient client, string query)
    {
        using var response = await client.Http.GetAsync($"{ReportsPath}?{query}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static Task<HttpResponseMessage> PutSettingsAsync(AuthClient client, string baseCurrency, int monthStartDay)
    {
        var body = new Dictionary<string, object?>
        {
            ["baseCurrency"] = baseCurrency,
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
