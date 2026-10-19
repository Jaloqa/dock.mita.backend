using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.Common.Budgets;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Features.Categories;

public sealed class CategoryBudgetsTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private const string CategoriesPath = "/v1/categories";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static FinancialPeriod Current => FinancialPeriod.For(Today, MonthStart.Day(1));

    [Fact]
    public async Task Should_MarkCategoryInUse_When_ItHasBudget()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var books = await TestData.AddCategoryAsync(factory, userId, name: "Books");
        await TestData.AddBudgetAsync(factory, userId, books.Id, Current.Key.ToString(), 10_000);

        var listed = await FindAsync(client, "Books");
        using var response = await PutAsync(client, books.Id, new { name = "Books", kind = "income", color = "chart-1" });

        listed.GetProperty("inUse").GetBoolean().ShouldBeTrue();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorsAsync(response))["kind"].ShouldBe(["validation.categoryKindLocked"]);
    }

    [Fact]
    public async Task Should_MoveBudgets_When_CategoryDeleted()
    {
        var previous = FinancialPeriod.Shift(Current.Key, -1, MonthStart.Day(1));
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var books = await TestData.AddCategoryAsync(factory, userId, name: "Books");
        await TestData.AddBudgetAsync(factory, userId, books.Id, previous.Key.ToString(), 10_000);
        var otherId = (await FindAsync(client, "Other")).GetProperty("id").GetGuid();

        using var response = await DeleteAsync(client, books.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var budget = (await BudgetsAsync(factory, userId, previous)).ShouldHaveSingleItem();
        budget.CategoryId.ShouldBe(otherId);
        budget.Limit.ShouldBe(10_000);
    }

    [Fact]
    public async Task Should_MergeLimits_When_ReassignedBudgetCollides()
    {
        var current = Current;
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId);
        var books = await TestData.AddCategoryAsync(factory, userId, name: "Books");
        var otherId = (await FindAsync(client, "Other")).GetProperty("id").GetGuid();
        await TestData.AddBudgetAsync(factory, userId, books.Id, current.Key.ToString(), 4_000);
        await TestData.AddBudgetAsync(factory, userId, otherId, current.Key.ToString(), 6_000);
        await TestData.AddTransactionAsync(factory, account, books, 1_500, Iso(Today));

        using var response = await DeleteAsync(client, books.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var budget = (await BudgetsAsync(factory, userId, current)).ShouldHaveSingleItem();
        budget.CategoryId.ShouldBe(otherId);
        budget.Limit.ShouldBe(10_000);
        budget.Spent.ShouldBe(1_500);
    }

    [Fact]
    public async Task Should_MergeInBaseCurrency_When_BudgetCurrenciesDiffer()
    {
        var current = Current;
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.EUR, Iso(current.From), 4.2717m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var books = await TestData.AddCategoryAsync(isolated, userId, name: "Books");
        var otherId = (await FindAsync(client, "Other")).GetProperty("id").GetGuid();
        await TestData.AddBudgetAsync(isolated, userId, books.Id, current.Key.ToString(), 4_000, Currency.EUR);
        await TestData.AddBudgetAsync(isolated, userId, otherId, current.Key.ToString(), 6_000);

        using var response = await DeleteAsync(client, books.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var budget = (await BudgetsAsync(isolated, userId, current)).ShouldHaveSingleItem();
        budget.CategoryId.ShouldBe(otherId);
        budget.Limit.ShouldBe(23_087);
        await using var scope = isolated.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Budgets.AsNoTracking().SingleAsync(row => row.Id == budget.Id, Ct);
        stored.Limit.ShouldBe(23_087);
        stored.Currency.ShouldBe(Currency.PLN);
    }

    [Fact]
    public async Task Should_KeepOneMinorUnitPerLimit_When_MergedLimitsRoundToZero()
    {
        var current = Current;
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(isolated, (Currency.UAH, Iso(current.From), 0.1m));
        using var client = new AuthClient(isolated);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var books = await TestData.AddCategoryAsync(isolated, userId, name: "Books");
        var otherId = (await FindAsync(client, "Other")).GetProperty("id").GetGuid();
        await TestData.AddBudgetAsync(isolated, userId, books.Id, current.Key.ToString(), 1, Currency.UAH);
        await TestData.AddBudgetAsync(isolated, userId, otherId, current.Key.ToString(), 1, Currency.UAH);

        using var response = await DeleteAsync(client, books.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var scope = isolated.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Budgets.AsNoTracking().SingleAsync(row => row.UserId == userId, Ct);
        stored.CategoryId.ShouldBe(otherId);
        stored.Limit.ShouldBe(2);
        stored.Currency.ShouldBe(Currency.PLN);
    }

    [Fact]
    public async Task Should_LeaveOtherBudgetsUntouched_When_NoCollision()
    {
        var current = Current;
        var previous = FinancialPeriod.Shift(current.Key, -1, MonthStart.Day(1));
        var next = FinancialPeriod.Shift(current.Key, 1, MonthStart.Day(1));
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var books = await TestData.AddCategoryAsync(factory, userId, name: "Books");
        var otherId = (await FindAsync(client, "Other")).GetProperty("id").GetGuid();
        await TestData.AddBudgetAsync(factory, userId, otherId, previous.Key.ToString(), 5_000);
        await TestData.AddBudgetAsync(factory, userId, otherId, next.Key.ToString(), 7_000);
        await TestData.AddBudgetAsync(factory, userId, books.Id, current.Key.ToString(), 3_000);

        using var response = await DeleteAsync(client, books.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await BudgetsAsync(factory, userId, previous)).ShouldHaveSingleItem().Limit.ShouldBe(5_000);
        (await BudgetsAsync(factory, userId, next)).ShouldHaveSingleItem().Limit.ShouldBe(7_000);
        var moved = (await BudgetsAsync(factory, userId, current)).ShouldHaveSingleItem();
        moved.CategoryId.ShouldBe(otherId);
        moved.Limit.ShouldBe(3_000);
    }

    private static async Task<IReadOnlyList<BudgetDto>> BudgetsAsync(
        WebApplicationFactory<Program> app,
        Guid userId,
        FinancialPeriod period)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var budgets = await db.BudgetsInAsync(userId, period, Currency.PLN, Today, Ct);
        return budgets.ShouldNotBeNull();
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> FindAsync(AuthClient client, string name)
    {
        using var response = await client.Http.GetAsync($"{CategoriesPath}?kind=expense", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        return page.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("name").GetString() == name);
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
