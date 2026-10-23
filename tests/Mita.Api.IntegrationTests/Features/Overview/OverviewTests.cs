using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Accounts;
using Mita.Domain.Categories;
using Mita.Domain.Debts;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Overview;

public sealed class OverviewTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string OverviewPath = "/v1/overview";

    private static readonly MonthStart FirstDay = MonthStart.Day(1);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Should_Return401_When_NotAuthenticated()
    {
        using var client = new AuthClient(factory);

        using var response = await client.Http.GetAsync(OverviewPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_SumMonth_AndCompareWithPrevious()
    {
        var today = Today;
        var current = FinancialPeriod.For(today, FirstDay);
        var previous = FinancialPeriod.Shift(current.Key, -1, FirstDay);
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId, openingBalance: 10_000);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        var housing = await TestData.AddCategoryAsync(factory, userId, name: "Monthly rent");
        var salary = await TestData.AddCategoryAsync(factory, userId, CategoryKind.Income, "Paycheck");
        await TestData.AddTransactionAsync(factory, account, groceries, 30_000, Iso(current.From));
        await TestData.AddTransactionAsync(factory, account, housing, 50_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, salary, 700_000, Iso(current.From));
        await TestData.AddTransactionAsync(factory, account, groceries, 40_000, Iso(previous.From));
        await TestData.AddTransactionAsync(factory, account, salary, 500_000, Iso(previous.From));
        await TestData.AddBudgetAsync(factory, userId, groceries.Id, current.Key.ToString(), 100_000);
        await TestData.AddGoalAsync(factory, userId, "Trip", 50_000, deadline: "2030-12");

        var overview = await GetOverviewAsync(client, string.Empty);

        Names(overview).ShouldBe(
            [
                "range", "from", "to", "previousFrom", "previousTo", "currency", "balance", "debts", "income",
                "expense", "averageExpense", "transactionCount", "spending", "month", "topBudgets", "recent", "goals",
                "goalCount", "hasTransactions"
            ],
            ignoreOrder: true);
        Names(overview.GetProperty("balance")).ShouldBe(["amount", "accounts"], ignoreOrder: true);
        Names(overview.GetProperty("debts")).ShouldBe(
            ["owedToMe", "owedByMe", "open", "overdue"],
            ignoreOrder: true);
        Names(overview.GetProperty("income")).ShouldBe(["amount", "count", "change"], ignoreOrder: true);
        Names(overview.GetProperty("expense")).ShouldBe(["amount", "count", "change"], ignoreOrder: true);
        Names(overview.GetProperty("spending")[0]).ShouldBe(["categoryId", "name", "amount", "percent"], ignoreOrder: true);
        Names(overview.GetProperty("month")).ShouldBe(
            ["period", "from", "to", "planned", "spent", "left", "budgets"],
            ignoreOrder: true);
        Names(overview.GetProperty("topBudgets")[0]).ShouldBe(
            ["id", "categoryId", "name", "limit", "spent", "remaining", "status"],
            ignoreOrder: true);
        Names(overview.GetProperty("recent")[0]).ShouldBe(
            ["id", "kind", "date", "title", "categoryId", "categoryName", "categoryColor", "accountName", "amount"],
            ignoreOrder: true);
        Names(overview.GetProperty("goals")[0]).ShouldBe(
            ["id", "name", "target", "saved", "percent", "status", "deadline"],
            ignoreOrder: true);
        overview.GetProperty("range").GetString().ShouldBe("month");
        overview.GetProperty("from").GetString().ShouldBe(Iso(current.From));
        overview.GetProperty("to").GetString().ShouldBe(Iso(current.To));
        overview.GetProperty("previousFrom").GetString().ShouldBe(Iso(previous.From));
        overview.GetProperty("previousTo").GetString().ShouldBe(Iso(previous.To));
        overview.GetProperty("currency").GetString().ShouldBe("PLN");
        Total(overview.GetProperty("income")).ShouldBe((700_000L, 1, (double?)40));
        Total(overview.GetProperty("expense")).ShouldBe((80_000L, 2, (double?)100));
        overview.GetProperty("averageExpense").GetInt64().ShouldBe(40_000);
        overview.GetProperty("transactionCount").GetInt32().ShouldBe(3);
        overview.GetProperty("hasTransactions").GetBoolean().ShouldBeTrue();
        overview.GetProperty("goals")[0].GetProperty("deadline").GetString().ShouldBe("2030-12");
    }

    [Fact]
    public async Task Should_SplitSpending_IntoTopFiveAndBucket()
    {
        var today = Today;
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var housing = await TestData.AddCategoryAsync(factory, userId, name: "Monthly rent");
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        var leisure = await TestData.AddCategoryAsync(factory, userId, name: "Fun nights");
        var cafes = await TestData.AddCategoryAsync(factory, userId, name: "Coffee shops");
        var transport = await TestData.AddCategoryAsync(factory, userId, name: "Bus tickets");
        var health = await TestData.AddCategoryAsync(factory, userId, name: "Pharmacy run");
        var shopping = await TestData.AddCategoryAsync(factory, userId, name: "Shops mall");
        var other = await SystemCategoryAsync(factory, userId, SystemCategory.Other);
        await TestData.AddTransactionAsync(factory, account, housing, 210_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, groceries, 124_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, leisure, 42_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, cafes, 41_200, Iso(today));
        await TestData.AddTransactionAsync(factory, account, transport, 31_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, health, 20_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, shopping, 15_370, Iso(today));
        await TestData.AddTransactionAsync(factory, account, other, 10_000, Iso(today));

        var overview = await GetOverviewAsync(client, string.Empty);

        var spending = overview.GetProperty("spending").EnumerateArray().ToList();
        spending.Count.ShouldBe(6);
        spending.Take(5).Select(slice => slice.GetProperty("categoryId").GetGuid()).ShouldBe(
            [housing.Id, groceries.Id, leisure.Id, cafes.Id, transport.Id]);
        spending.Take(5).Select(slice => slice.GetProperty("name").GetString()).ShouldBe(
            ["Monthly rent", "Weekly food", "Fun nights", "Coffee shops", "Bus tickets"]);
        spending.Select(slice => slice.GetProperty("amount").GetInt64()).ShouldBe(
            [210_000, 124_000, 42_000, 41_200, 31_000, 45_370]);
        spending.Select(slice => slice.GetProperty("percent").GetDouble()).ShouldBe([42.5, 25.1, 8.5, 8.4, 6.3, 9.2]);
        spending.Sum(slice => (int)Math.Round(slice.GetProperty("percent").GetDouble() * 10)).ShouldBe(1000);
        spending[5].GetProperty("categoryId").ValueKind.ShouldBe(JsonValueKind.Null);
        spending[5].GetProperty("name").ValueKind.ShouldBe(JsonValueKind.Null);
        var expense = overview.GetProperty("expense");
        expense.GetProperty("amount").GetInt64().ShouldBe(493_570);
        expense.GetProperty("count").GetInt32().ShouldBe(8);
    }

    [Fact]
    public async Task Should_CoverWeekAndYear()
    {
        var today = Today;
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        await TestData.AddTransactionAsync(factory, account, groceries, 1_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, groceries, 2_000, Iso(today.AddDays(-6)));
        await TestData.AddTransactionAsync(factory, account, groceries, 4_000, Iso(today.AddDays(-7)));
        await TestData.AddTransactionAsync(factory, account, groceries, 8_000, Iso(today.AddDays(-13)));
        await TestData.AddTransactionAsync(factory, account, groceries, 16_000, Iso(today.AddDays(-14)));

        var week = await GetOverviewAsync(client, "period=week");

        week.GetProperty("range").GetString().ShouldBe("week");
        week.GetProperty("from").GetString().ShouldBe(Iso(today.AddDays(-6)));
        week.GetProperty("to").GetString().ShouldBe(Iso(today));
        week.GetProperty("previousFrom").GetString().ShouldBe(Iso(today.AddDays(-13)));
        week.GetProperty("previousTo").GetString().ShouldBe(Iso(today.AddDays(-7)));
        Total(week.GetProperty("expense")).ShouldBe((3_000L, 2, (double?)-75));

        var current = FinancialPeriod.For(today, FirstDay);
        var yearStart = FinancialPeriod.Shift(current.Key, -11, FirstDay).From;
        using var fresh = new AuthClient(factory);
        await fresh.RegisterNewAsync(Ct);
        var freshId = await UserIdAsync(fresh);
        var freshAccount = await TestData.AddAccountAsync(factory, freshId);
        var freshGroceries = await TestData.AddCategoryAsync(factory, freshId, name: "Weekly food");
        await TestData.AddTransactionAsync(factory, freshAccount, freshGroceries, 1_000, Iso(yearStart));
        await TestData.AddTransactionAsync(factory, freshAccount, freshGroceries, 2_000, Iso(yearStart.AddDays(-1)));

        var year = await GetOverviewAsync(fresh, "period=year");

        year.GetProperty("range").GetString().ShouldBe("year");
        year.GetProperty("from").GetString().ShouldBe(Iso(yearStart));
        year.GetProperty("to").GetString().ShouldBe(Iso(current.To));
        year.GetProperty("previousFrom").GetString().ShouldBe(Iso(FinancialPeriod.Shift(current.Key, -23, FirstDay).From));
        year.GetProperty("previousTo").GetString().ShouldBe(Iso(yearStart.AddDays(-1)));
        Total(year.GetProperty("expense")).ShouldBe((1_000L, 1, (double?)-50));
    }

    [Fact]
    public async Task Should_KeepMonthOnCurrentPeriod_WithTopBudgets()
    {
        var today = Today;
        var current = FinancialPeriod.For(today, FirstDay);
        var key = current.Key.ToString();
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        var cafes = await TestData.AddCategoryAsync(factory, userId, name: "Coffee shops");
        var housing = await TestData.AddCategoryAsync(factory, userId, name: "Monthly rent");
        var sport = await TestData.AddCategoryAsync(factory, userId, name: "Gym pass");
        var transport = await TestData.AddCategoryAsync(factory, userId, name: "Bus tickets");
        var leisure = await TestData.AddCategoryAsync(factory, userId, name: "Fun nights");
        await TestData.AddBudgetAsync(factory, userId, groceries.Id, key, 100_000);
        await TestData.AddBudgetAsync(factory, userId, cafes.Id, key, 10_000);
        await TestData.AddBudgetAsync(factory, userId, housing.Id, key, 20_000);
        await TestData.AddBudgetAsync(factory, userId, sport.Id, key, 50_000);
        await TestData.AddBudgetAsync(factory, userId, transport.Id, key, 30_000);
        await TestData.AddBudgetAsync(factory, userId, leisure.Id, key, 40_000);
        await TestData.AddTransactionAsync(factory, account, groceries, 50_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, cafes, 9_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, housing, 25_000, Iso(today));

        var overview = await GetOverviewAsync(client, "period=week");

        var month = overview.GetProperty("month");
        month.GetProperty("period").GetString().ShouldBe(key);
        month.GetProperty("from").GetString().ShouldBe(Iso(current.From));
        month.GetProperty("to").GetString().ShouldBe(Iso(current.To));
        month.GetProperty("planned").GetInt64().ShouldBe(250_000);
        month.GetProperty("spent").GetInt64().ShouldBe(84_000);
        month.GetProperty("left").GetInt64().ShouldBe(166_000);
        month.GetProperty("budgets").GetInt32().ShouldBe(6);
        var top = overview.GetProperty("topBudgets").EnumerateArray().ToList();
        top.Select(budget => (
                budget.GetProperty("name").GetString(),
                budget.GetProperty("status").GetString(),
                budget.GetProperty("remaining").GetInt64()))
            .ShouldBe(
            [
                ("Monthly rent", "over_budget", -5_000L),
                ("Coffee shops", "almost_spent", 1_000L),
                ("Weekly food", "on_track", 50_000L),
                ("Gym pass", "on_track", 50_000L),
                ("Bus tickets", "on_track", 30_000L)
            ]);
        top[0].GetProperty("categoryId").GetGuid().ShouldBe(housing.Id);
        top[0].GetProperty("limit").GetInt64().ShouldBe(20_000);
        top[0].GetProperty("spent").GetInt64().ShouldBe(25_000);
    }

    [Fact]
    public async Task Should_TotalBalances_OfAccountsHoldingMoney()
    {
        var today = Today;
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, Iso(today), 4m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var card = await TestData.AddAccountAsync(isolated, userId, openingBalance: 100_000);
        await TestData.AddAccountAsync(isolated, userId, Currency.EUR, 1_000, type: AccountType.Savings);
        var cash = await TestData.AddAccountAsync(isolated, userId, openingBalance: 5_000, type: AccountType.Cash);

        Balance(await GetOverviewAsync(client, string.Empty)).ShouldBe((109_000L, 3));

        var groceries = await TestData.AddCategoryAsync(isolated, userId, name: "Weekly food");
        await TestData.AddTransactionAsync(isolated, card, groceries, 12_345, Iso(today));

        Balance(await GetOverviewAsync(client, string.Empty)).ShouldBe((96_655L, 3));

        await ArchiveAsync(isolated, cash.Id);

        Balance(await GetOverviewAsync(client, string.Empty)).ShouldBe((96_655L, 3));

        await TestData.AddAccountAsync(isolated, userId, archived: true);

        Balance(await GetOverviewAsync(client, string.Empty)).ShouldBe((96_655L, 3));
    }

    [Fact]
    public async Task Should_ListNewestTransactions_OfTheRange()
    {
        var today = Today;
        var current = FinancialPeriod.For(today, FirstDay);
        var previous = FinancialPeriod.Shift(current.Key, -1, FirstDay);
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId, openingBalance: 100_000);
        var savings = await TestData.AddAccountAsync(factory, userId);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        var transfer = await SystemCategoryAsync(factory, userId, SystemCategory.Transfer);
        await TestData.AddTransactionAsync(factory, account, groceries, 500, Iso(previous.From), "old");
        var created = new List<Guid>();
        for (var index = 1; index <= 8; index++)
        {
            var transaction = await TestData.AddTransactionAsync(factory, account, groceries, index * 1_000, Iso(today), $"t{index}");
            created.Add(transaction.Id);
        }

        await TestData.AddTransferAsync(factory, account, savings, 25_000);
        await TestData.AddTransactionAsync(factory, account, transfer, 25_000, Iso(today), "moved");

        var overview = await GetOverviewAsync(client, string.Empty);

        var recent = overview.GetProperty("recent").EnumerateArray().ToList();
        recent.Select(item => item.GetProperty("title").GetString()).ShouldBe(["t8", "t7", "t6", "t5", "t4", "t3"]);
        var newest = recent[0];
        newest.GetProperty("id").GetGuid().ShouldBe(created[^1]);
        newest.GetProperty("kind").GetString().ShouldBe("expense");
        newest.GetProperty("date").GetString().ShouldBe(Iso(today));
        newest.GetProperty("title").GetString().ShouldBe("t8");
        newest.GetProperty("categoryId").GetGuid().ShouldBe(groceries.Id);
        newest.GetProperty("categoryName").GetString().ShouldBe("Weekly food");
        newest.GetProperty("categoryColor").GetString().ShouldBe("chart-1");
        newest.GetProperty("accountName").GetString().ShouldBe(account.Name);
        newest.GetProperty("amount").GetInt64().ShouldBe(8_000);
    }

    [Fact]
    public async Task Should_PreviewThreeGoals_NotReachedFirst()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        await TestData.AddGoalAsync(factory, userId, "A", 10_000, 10_000);
        var b = await TestData.AddGoalAsync(factory, userId, "B", 20_000);
        await TestData.AddGoalAsync(factory, userId, "C", 30_000);
        await TestData.AddGoalAsync(factory, userId, "D", 40_000);

        var overview = await GetOverviewAsync(client, string.Empty);

        var goals = overview.GetProperty("goals").EnumerateArray().ToList();
        goals.Select(goal => goal.GetProperty("name").GetString()).ShouldBe(["B", "C", "D"]);
        var first = goals[0];
        first.GetProperty("id").GetGuid().ShouldBe(b.Id);
        first.GetProperty("name").GetString().ShouldBe("B");
        first.GetProperty("target").GetInt64().ShouldBe(20_000);
        first.GetProperty("saved").GetInt64().ShouldBe(0);
        first.GetProperty("percent").GetInt32().ShouldBe(0);
        first.GetProperty("status").GetString().ShouldBe("active");
        first.GetProperty("deadline").ValueKind.ShouldBe(JsonValueKind.Null);
        overview.GetProperty("goalCount").GetInt32().ShouldBe(4);
    }

    [Fact]
    public async Task Should_RepriceOverview_When_BaseCurrencyChanges()
    {
        var today = Today;
        var current = FinancialPeriod.For(today, FirstDay);
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, Iso(today), 4m), (Currency.EUR, Iso(current.From), 4m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var card = await TestData.AddAccountAsync(isolated, userId, openingBalance: 40_000);
        var groceries = await TestData.AddCategoryAsync(isolated, userId, name: "Weekly food");
        await TestData.AddTransactionAsync(isolated, card, groceries, 10_000, Iso(today));
        await TestData.AddBudgetAsync(isolated, userId, groceries.Id, current.Key.ToString(), 100_000);
        await TestData.AddGoalAsync(isolated, userId, "Trip", 800_000);
        using (var put = await PutSettingsAsync(client, "EUR", 1))
        {
            put.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var overview = await GetOverviewAsync(client, string.Empty);

        overview.GetProperty("currency").GetString().ShouldBe("EUR");
        overview.GetProperty("expense").GetProperty("amount").GetInt64().ShouldBe(2_500);
        overview.GetProperty("spending")[0].GetProperty("amount").GetInt64().ShouldBe(2_500);
        overview.GetProperty("recent")[0].GetProperty("amount").GetInt64().ShouldBe(2_500);
        overview.GetProperty("balance").GetProperty("amount").GetInt64().ShouldBe(7_500);
        overview.GetProperty("month").GetProperty("planned").GetInt64().ShouldBe(25_000);
        overview.GetProperty("goals")[0].GetProperty("target").GetInt64().ShouldBe(200_000);
    }

    [Fact]
    public async Task Should_ValueAccount_When_RateIsStale()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.KWD, Iso(Today.AddDays(-90)), 12m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        await TestData.AddAccountAsync(isolated, userId, Currency.KWD, 1_000);

        Balance(await GetOverviewAsync(client, string.Empty)).ShouldBe((1_200L, 1));
    }

    [Fact]
    public async Task Should_Return503_When_BalanceNeedsMissingRate()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        await TestData.AddAccountAsync(isolated, userId, Currency.EUR, 1_000);

        using var response = await client.Http.GetAsync(OverviewPath, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("rates.unavailable");
    }

    [Fact]
    public async Task Should_SummarizeDebts()
    {
        var today = Today;
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, Iso(today), 4.30m));
        using var owner = new AuthClient(isolated);
        using var neighbour = new AuthClient(isolated);
        using var fresh = new AuthClient(isolated);
        await owner.RegisterNewAsync(Ct);
        await neighbour.RegisterNewAsync(Ct);
        await fresh.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var neighbourId = await UserIdAsync(neighbour);
        var start = Iso(today.AddDays(-10));
        await TestData.AddDebtAsync(
            isolated, ownerId, DebtDirection.Lent, 40_000, date: start, dueDate: Iso(today.AddDays(5)));
        await TestData.AddDebtAsync(isolated, ownerId, DebtDirection.Lent, 5_000, Currency.EUR, date: start);
        await TestData.AddDebtAsync(
            isolated, ownerId, DebtDirection.Borrowed, 150_000, date: start, dueDate: Iso(today.AddDays(-1)));
        var settled = await TestData.AddDebtAsync(isolated, ownerId, DebtDirection.Lent, 12_000, date: start);
        await TestData.AddRepaymentAsync(isolated, settled, 12_000, date: Iso(today));
        await TestData.AddDebtAsync(isolated, neighbourId, DebtDirection.Lent, 9_000, date: start, dueDate: Iso(today.AddDays(-3)));
        await TestData.AddDebtAsync(isolated, neighbourId, DebtDirection.Borrowed, 1_000, date: start);

        var own = await GetOverviewAsync(owner, string.Empty);
        var empty = await GetOverviewAsync(fresh, string.Empty);

        Debts(own).ShouldBe((61_500L, 150_000L, 3, 1));
        Debts(empty).ShouldBe((0L, 0L, 0, 0));
        Debts(await GetOverviewAsync(neighbour, string.Empty)).ShouldBe((9_000L, 1_000L, 2, 1));
    }

    [Fact]
    public async Task Should_NotCountDebts_InTotals()
    {
        var today = Today;
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var lender = await TestData.AddAccountAsync(factory, userId, openingBalance: 100_000);
        var borrower = await TestData.AddAccountAsync(factory, userId, openingBalance: 20_000);
        var lent = await TestData.AddDebtAsync(
            factory, userId, DebtDirection.Lent, 30_000, account: lender, date: Iso(today));
        var borrowed = await TestData.AddDebtAsync(
            factory, userId, DebtDirection.Borrowed, 50_000, account: borrower, date: Iso(today));
        await TestData.AddRepaymentAsync(factory, lent, 10_000, lender, Iso(today));
        await TestData.AddRepaymentAsync(factory, borrowed, 20_000, borrower, Iso(today));

        var overview = await GetOverviewAsync(client, string.Empty);

        overview.GetProperty("income").GetProperty("amount").GetInt64().ShouldBe(0);
        overview.GetProperty("expense").GetProperty("amount").GetInt64().ShouldBe(0);
        overview.GetProperty("transactionCount").GetInt32().ShouldBe(0);
        overview.GetProperty("averageExpense").ValueKind.ShouldBe(JsonValueKind.Null);
        overview.GetProperty("spending").GetArrayLength().ShouldBe(0);
        overview.GetProperty("recent").GetArrayLength().ShouldBe(0);
        overview.GetProperty("hasTransactions").GetBoolean().ShouldBeFalse();
        var balances = await AccountBalancesAsync(client);
        balances[lender.Id].ShouldBe(80_000);
        balances[borrower.Id].ShouldBe(50_000);
        Balance(overview).ShouldBe((balances.Values.Sum(), 2));
        Debts(overview).ShouldBe((20_000L, 30_000L, 2, 0));
    }

    [Fact]
    public async Task Should_RepriceDebts_When_BaseCurrencyChanges()
    {
        var today = Today;
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, Iso(today), 4.30m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 43_000, date: Iso(today));
        Debts(await GetOverviewAsync(client, string.Empty)).ShouldBe((43_000L, 0L, 1, 0));
        using (var put = await PutSettingsAsync(client, "EUR", 1))
        {
            put.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var overview = await GetOverviewAsync(client, string.Empty);

        overview.GetProperty("currency").GetString().ShouldBe("EUR");
        Debts(overview).ShouldBe((10_000L, 0L, 1, 0));
    }

    [Fact]
    public async Task Should_Return503_When_DebtNeedsMissingRate()
    {
        await using var isolated = factory.WithIsolatedData();
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var debt = await TestData.AddDebtAsync(isolated, userId, DebtDirection.Lent, 5_000, Currency.KWD, date: Iso(Today));

        using var missing = await client.Http.GetAsync(OverviewPath, Ct);

        missing.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var problem = await missing.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("code").GetString().ShouldBe("rates.unavailable");

        await TestData.AddRepaymentAsync(isolated, debt, 5_000);

        Debts(await GetOverviewAsync(client, string.Empty)).ShouldBe((0L, 0L, 0, 0));
    }

    [Fact]
    public async Task Should_FollowMonthStart_ForMonthAndYear()
    {
        var today = Today;
        var shifted = MonthStart.Day(10);
        var period = FinancialPeriod.For(today, shifted);
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var groceries = await TestData.AddCategoryAsync(factory, userId, name: "Weekly food");
        await TestData.AddTransactionAsync(factory, account, groceries, 2_000, Iso(period.From.AddDays(-1)));
        await TestData.AddTransactionAsync(factory, account, groceries, 3_000, Iso(period.From));
        using (var put = await PutSettingsAsync(client, "PLN", 10))
        {
            put.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var monthView = await GetOverviewAsync(client, "period=month");
        var yearView = await GetOverviewAsync(client, "period=year");

        monthView.GetProperty("from").GetString().ShouldBe(Iso(period.From));
        monthView.GetProperty("to").GetString().ShouldBe(Iso(period.To));
        Total(monthView.GetProperty("expense")).ShouldBe((3_000L, 1, (double?)50));
        var month = monthView.GetProperty("month");
        month.GetProperty("period").GetString().ShouldBe(period.Key.ToString());
        month.GetProperty("from").GetString().ShouldBe(Iso(period.From));
        month.GetProperty("to").GetString().ShouldBe(Iso(period.To));
        yearView.GetProperty("from").GetString().ShouldBe(Iso(FinancialPeriod.Shift(period.Key, -11, shifted).From));
        yearView.GetProperty("to").GetString().ShouldBe(Iso(period.To));
        yearView.GetProperty("previousFrom").GetString().ShouldBe(Iso(FinancialPeriod.Shift(period.Key, -23, shifted).From));
        yearView.GetProperty("previousTo").GetString().ShouldBe(Iso(FinancialPeriod.Shift(period.Key, -12, shifted).To));
    }

    [Fact]
    public async Task Should_ReturnEmptyOverview_And_IsolateUsers()
    {
        var today = Today;
        var current = FinancialPeriod.For(today, FirstDay);
        using var owner = new AuthClient(factory);
        await owner.RegisterNewAsync(Ct);
        using var intruder = new AuthClient(factory);
        await intruder.RegisterNewAsync(Ct);
        var ownerId = await UserIdAsync(owner);
        var account = await TestData.AddAccountAsync(factory, ownerId, openingBalance: 50_000);
        var groceries = await TestData.AddCategoryAsync(factory, ownerId, name: "Weekly food");
        var salary = await TestData.AddCategoryAsync(factory, ownerId, CategoryKind.Income, "Paycheck");
        await TestData.AddTransactionAsync(factory, account, groceries, 10_000, Iso(today));
        await TestData.AddTransactionAsync(factory, account, salary, 30_000, Iso(today));
        await TestData.AddBudgetAsync(factory, ownerId, groceries.Id, current.Key.ToString(), 20_000);
        await TestData.AddGoalAsync(factory, ownerId, "Trip", 50_000);

        var overview = await GetOverviewAsync(intruder, string.Empty);

        Balance(overview).ShouldBe((0L, 0));
        Total(overview.GetProperty("income")).ShouldBe((0L, 0, (double?)null));
        Total(overview.GetProperty("expense")).ShouldBe((0L, 0, (double?)null));
        overview.GetProperty("income").GetProperty("change").ValueKind.ShouldBe(JsonValueKind.Null);
        overview.GetProperty("averageExpense").ValueKind.ShouldBe(JsonValueKind.Null);
        overview.GetProperty("transactionCount").GetInt32().ShouldBe(0);
        overview.GetProperty("spending").GetArrayLength().ShouldBe(0);
        overview.GetProperty("topBudgets").GetArrayLength().ShouldBe(0);
        overview.GetProperty("recent").GetArrayLength().ShouldBe(0);
        overview.GetProperty("goals").GetArrayLength().ShouldBe(0);
        overview.GetProperty("goalCount").GetInt32().ShouldBe(0);
        overview.GetProperty("hasTransactions").GetBoolean().ShouldBeFalse();
        var month = overview.GetProperty("month");
        month.GetProperty("period").GetString().ShouldBe(current.Key.ToString());
        month.GetProperty("planned").GetInt64().ShouldBe(0);
        month.GetProperty("spent").GetInt64().ShouldBe(0);
        month.GetProperty("left").GetInt64().ShouldBe(0);
        month.GetProperty("budgets").GetInt32().ShouldBe(0);

        var own = await GetOverviewAsync(owner, string.Empty);

        Balance(own).ShouldBe((70_000L, 1));
        own.GetProperty("transactionCount").GetInt32().ShouldBe(2);
        own.GetProperty("topBudgets").GetArrayLength().ShouldBe(1);
        own.GetProperty("goalCount").GetInt32().ShouldBe(1);
        own.GetProperty("hasTransactions").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData("period=day")]
    [InlineData("period=")]
    public async Task Should_RejectUnknownPeriod(string query)
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);

        using var response = await client.Http.GetAsync($"{OverviewPath}?{query}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        problem.GetProperty("errors").GetProperty("period").EnumerateArray().Select(error => error.GetString())
            .ShouldBe(["validation.invalidValue"]);
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static List<string> Names(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name)];

    private static (long Amount, int Count, double? Change) Total(JsonElement total) =>
        (
            total.GetProperty("amount").GetInt64(),
            total.GetProperty("count").GetInt32(),
            total.GetProperty("change") is { ValueKind: JsonValueKind.Null } ? null : total.GetProperty("change").GetDouble());

    private static (long Amount, int Accounts) Balance(JsonElement overview)
    {
        var balance = overview.GetProperty("balance");
        return (balance.GetProperty("amount").GetInt64(), balance.GetProperty("accounts").GetInt32());
    }

    private static (long OwedToMe, long OwedByMe, int Open, int Overdue) Debts(JsonElement overview)
    {
        var debts = overview.GetProperty("debts");
        return (
            debts.GetProperty("owedToMe").GetInt64(),
            debts.GetProperty("owedByMe").GetInt64(),
            debts.GetProperty("open").GetInt32(),
            debts.GetProperty("overdue").GetInt32());
    }

    private static async Task<Dictionary<Guid, long>> AccountBalancesAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/accounts", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetGuid(),
            item => item.GetProperty("balance").GetInt64());
    }

    private static async Task<Category> SystemCategoryAsync(
        WebApplicationFactory<Program> target,
        Guid userId,
        SystemCategory system)
    {
        await using var scope = target.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Categories.AsNoTracking().SingleAsync(
            category => category.UserId == userId && category.System == system && category.Kind == CategoryKind.Expense,
            Ct);
    }

    private static async Task ArchiveAsync(WebApplicationFactory<Program> target, Guid accountId)
    {
        await using var scope = target.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await db.Accounts.SingleAsync(row => row.Id == accountId, Ct);
        account.Update(account.Name, account.Type, account.Currency, account.OpeningBalance, account.CardLast4, true);
        await db.SaveChangesAsync(Ct);
    }

    private static async Task<JsonElement> GetOverviewAsync(AuthClient client, string query)
    {
        using var response = await client.Http.GetAsync($"{OverviewPath}?{query}", Ct);
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
