using System.Net;
using System.Text.Json;
using Mita.Api.IntegrationTests.Infrastructure;
using Shouldly;

namespace Mita.Api.IntegrationTests;

public sealed class OpenApiTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    [Fact]
    public async Task Should_ServeOpenApiDocument()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var info = document.RootElement.GetProperty("info");
        info.GetProperty("title").GetString().ShouldBe("Mita API");
        info.GetProperty("version").GetString().ShouldBe("v1");
    }

    [Fact]
    public async Task Should_ListVersionedPaths_WithoutVersionTemplate()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/system/ping", out _).ShouldBeTrue();
        paths.EnumerateObject().ShouldNotContain(path => path.Name.Contains("{version}"));
    }

    [Fact]
    public async Task Should_DescribeAuthUsersAndSettingsEndpoints()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        paths.TryGetProperty("/v1/auth/login", out var login).ShouldBeTrue();
        paths.TryGetProperty("/v1/me", out var me).ShouldBeTrue();
        paths.TryGetProperty("/v1/settings", out var settings).ShouldBeTrue();

        var schemes = root.GetProperty("components").GetProperty("securitySchemes");
        var cookie = schemes.GetProperty("cookie");
        cookie.GetProperty("type").GetString().ShouldBe("apiKey");
        cookie.GetProperty("in").GetString().ShouldBe("cookie");
        cookie.GetProperty("name").GetString().ShouldBe("mita_at");
        var bearer = schemes.GetProperty("bearer");
        bearer.GetProperty("type").GetString().ShouldBe("http");
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");

        login.GetProperty("post").TryGetProperty("security", out _).ShouldBeFalse();
        me.GetProperty("get").GetProperty("security").GetArrayLength().ShouldBe(2);
        settings.GetProperty("put").GetProperty("security").GetArrayLength().ShouldBe(2);

        TagOf(login.GetProperty("post")).ShouldBe("Auth");
        TagOf(me.GetProperty("get")).ShouldBe("Users");
        TagOf(settings.GetProperty("get")).ShouldBe("Settings");
    }

    [Fact]
    public async Task Should_DescribeAccountsTransfersAndRates()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/accounts", out var accounts).ShouldBeTrue();
        paths.TryGetProperty("/v1/accounts/{id}", out var account).ShouldBeTrue();
        paths.TryGetProperty("/v1/transfers", out var transfers).ShouldBeTrue();
        paths.TryGetProperty("/v1/rates", out var rates).ShouldBeTrue();

        TagOf(accounts.GetProperty("get")).ShouldBe("Accounts");
        TagOf(accounts.GetProperty("post")).ShouldBe("Accounts");
        TagOf(account.GetProperty("put")).ShouldBe("Accounts");
        TagOf(account.GetProperty("delete")).ShouldBe("Accounts");
        TagOf(transfers.GetProperty("get")).ShouldBe("Transfers");
        TagOf(transfers.GetProperty("post")).ShouldBe("Transfers");
        TagOf(rates.GetProperty("get")).ShouldBe("Rates");
    }

    [Fact]
    public async Task Should_DescribeRatesQuery()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/rates", out var rates).ShouldBeTrue();

        var parameters = ParameterNames(rates.GetProperty("get"));
        parameters.ShouldContain("date");
        parameters.ShouldContain("from");
        parameters.ShouldContain("to");
    }

    [Fact]
    public async Task Should_DescribeCategoriesAndTransactions()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/categories", out var categories).ShouldBeTrue();
        paths.TryGetProperty("/v1/categories/{id}", out var category).ShouldBeTrue();
        paths.TryGetProperty("/v1/transactions", out var transactions).ShouldBeTrue();
        paths.TryGetProperty("/v1/transactions/{id}", out var transaction).ShouldBeTrue();

        TagOf(categories.GetProperty("get")).ShouldBe("Categories");
        TagOf(categories.GetProperty("post")).ShouldBe("Categories");
        TagOf(category.GetProperty("put")).ShouldBe("Categories");
        TagOf(category.GetProperty("delete")).ShouldBe("Categories");
        TagOf(transactions.GetProperty("get")).ShouldBe("Transactions");
        TagOf(transactions.GetProperty("post")).ShouldBe("Transactions");
        TagOf(transaction.GetProperty("get")).ShouldBe("Transactions");
        TagOf(transaction.GetProperty("put")).ShouldBe("Transactions");
        TagOf(transaction.GetProperty("delete")).ShouldBe("Transactions");
    }

    [Fact]
    public async Task Should_DescribeTags()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/tags", out var tags).ShouldBeTrue();
        paths.TryGetProperty("/v1/tags/{id}", out var tag).ShouldBeTrue();
        paths.TryGetProperty("/v1/transactions", out var transactions).ShouldBeTrue();

        TagOf(tags.GetProperty("get")).ShouldBe("Tags");
        TagOf(tag.GetProperty("put")).ShouldBe("Tags");
        TagOf(tag.GetProperty("delete")).ShouldBe("Tags");
        ParameterNames(transactions.GetProperty("get")).ShouldContain("tagIds");
    }

    [Fact]
    public async Task Should_DescribeBudgetsAndGoals()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/budgets", out var budgets).ShouldBeTrue();
        paths.TryGetProperty("/v1/budgets/{id}", out var budget).ShouldBeTrue();
        paths.TryGetProperty("/v1/budgets/copy", out var copy).ShouldBeTrue();
        paths.TryGetProperty("/v1/goals", out var goals).ShouldBeTrue();
        paths.TryGetProperty("/v1/goals/{id}", out var goal).ShouldBeTrue();
        paths.TryGetProperty("/v1/goals/{id}/contributions", out var contributions).ShouldBeTrue();

        TagOf(budgets.GetProperty("get")).ShouldBe("Budgets");
        TagOf(budgets.GetProperty("post")).ShouldBe("Budgets");
        TagOf(budget.GetProperty("put")).ShouldBe("Budgets");
        TagOf(budget.GetProperty("delete")).ShouldBe("Budgets");
        TagOf(copy.GetProperty("post")).ShouldBe("Budgets");
        TagOf(goals.GetProperty("get")).ShouldBe("Goals");
        TagOf(goals.GetProperty("post")).ShouldBe("Goals");
        TagOf(goal.GetProperty("put")).ShouldBe("Goals");
        TagOf(goal.GetProperty("delete")).ShouldBe("Goals");
        TagOf(contributions.GetProperty("post")).ShouldBe("Goals");
    }

    [Fact]
    public async Task Should_DescribeDebts()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/debts", out var debts).ShouldBeTrue();
        paths.TryGetProperty("/v1/debts/{id}", out var debt).ShouldBeTrue();
        paths.TryGetProperty("/v1/debts/{id}/repayments", out var repayments).ShouldBeTrue();
        paths.TryGetProperty("/v1/debts/{id}/repayments/{repaymentId}", out var repayment).ShouldBeTrue();

        TagOf(debts.GetProperty("get")).ShouldBe("Debts");
        TagOf(debts.GetProperty("post")).ShouldBe("Debts");
        ParameterNames(debts.GetProperty("get")).ShouldContain("status");
        TagOf(debt.GetProperty("put")).ShouldBe("Debts");
        TagOf(debt.GetProperty("delete")).ShouldBe("Debts");
        TagOf(repayments.GetProperty("get")).ShouldBe("Debts");
        TagOf(repayments.GetProperty("post")).ShouldBe("Debts");
        TagOf(repayment.GetProperty("delete")).ShouldBe("Debts");
        ParameterNames(repayments.GetProperty("get")).ShouldContain("pageSize");
    }

    [Fact]
    public async Task Should_DescribeGroups()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/groups", out var groups).ShouldBeTrue();
        paths.TryGetProperty("/v1/groups/{id}", out var group).ShouldBeTrue();
        paths.TryGetProperty("/v1/groups/{id}/members", out var members).ShouldBeTrue();
        paths.TryGetProperty("/v1/groups/{id}/members/{memberId}", out var member).ShouldBeTrue();
        paths.TryGetProperty("/v1/groups/{id}/categories", out var categories).ShouldBeTrue();
        paths.TryGetProperty("/v1/groups/{id}/expenses", out var expenses).ShouldBeTrue();
        paths.TryGetProperty("/v1/groups/{id}/expenses/{expenseId}", out var expense).ShouldBeTrue();
        paths.TryGetProperty("/v1/groups/{id}/expenses/{expenseId}/restore", out var restore).ShouldBeTrue();
        paths.TryGetProperty("/v1/groups/{id}/expenses/{expenseId}/history", out var history).ShouldBeTrue();
        paths.TryGetProperty("/v1/groups/{id}/repayments", out var groupRepayments).ShouldBeTrue();
        paths.TryGetProperty("/v1/groups/{id}/repayments/{repaymentId}", out var groupRepayment).ShouldBeTrue();
        paths.TryGetProperty("/v1/groups/{id}/repayments/{repaymentId}/restore", out var restoreRepayment).ShouldBeTrue();
        paths.TryGetProperty("/v1/groups/{id}/repayments/{repaymentId}/history", out var repaymentHistory).ShouldBeTrue();

        TagOf(groups.GetProperty("get")).ShouldBe("Groups");
        TagOf(groups.GetProperty("post")).ShouldBe("Groups");
        TagOf(group.GetProperty("get")).ShouldBe("Groups");
        TagOf(group.GetProperty("put")).ShouldBe("Groups");
        TagOf(group.GetProperty("delete")).ShouldBe("Groups");
        ParameterNames(groups.GetProperty("get")).ShouldContain("pageSize");
        TagOf(members.GetProperty("post")).ShouldBe("Groups");
        TagOf(member.GetProperty("put")).ShouldBe("Groups");
        TagOf(member.GetProperty("delete")).ShouldBe("Groups");
        TagOf(categories.GetProperty("post")).ShouldBe("Groups");
        TagOf(expenses.GetProperty("get")).ShouldBe("Groups");
        TagOf(expenses.GetProperty("post")).ShouldBe("Groups");
        ParameterNames(expenses.GetProperty("get")).ShouldContain("deleted");
        ParameterNames(expenses.GetProperty("get")).ShouldContain("pageSize");
        TagOf(expense.GetProperty("put")).ShouldBe("Groups");
        TagOf(expense.GetProperty("delete")).ShouldBe("Groups");
        TagOf(restore.GetProperty("post")).ShouldBe("Groups");
        TagOf(history.GetProperty("get")).ShouldBe("Groups");
        ParameterNames(history.GetProperty("get")).ShouldContain("pageSize");
        TagOf(groupRepayments.GetProperty("get")).ShouldBe("Groups");
        TagOf(groupRepayments.GetProperty("post")).ShouldBe("Groups");
        ParameterNames(groupRepayments.GetProperty("get")).ShouldContain("deleted");
        ParameterNames(groupRepayments.GetProperty("get")).ShouldContain("pageSize");
        TagOf(groupRepayment.GetProperty("put")).ShouldBe("Groups");
        TagOf(groupRepayment.GetProperty("delete")).ShouldBe("Groups");
        TagOf(restoreRepayment.GetProperty("post")).ShouldBe("Groups");
        TagOf(repaymentHistory.GetProperty("get")).ShouldBe("Groups");
        ParameterNames(repaymentHistory.GetProperty("get")).ShouldContain("pageSize");
    }

    [Fact]
    public async Task Should_DescribeOverviewAndReports()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var paths = document.RootElement.GetProperty("paths");
        paths.TryGetProperty("/v1/overview", out var overview).ShouldBeTrue();
        paths.TryGetProperty("/v1/reports", out var reports).ShouldBeTrue();

        TagOf(overview.GetProperty("get")).ShouldBe("Overview");
        TagOf(reports.GetProperty("get")).ShouldBe("Reports");
        ParameterNames(overview.GetProperty("get")).ShouldContain("period");
        ParameterNames(reports.GetProperty("get")).ShouldContain("range");
    }

    private static string?[] ParameterNames(JsonElement operation) =>
        operation.GetProperty("parameters").EnumerateArray()
            .Select(parameter => parameter.GetProperty("name").GetString())
            .ToArray();

    private static string? TagOf(JsonElement operation) =>
        operation.GetProperty("tags")[0].GetString();

    [Fact]
    public async Task Should_NotServeScalar_OutsideDevelopment()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/scalar", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
