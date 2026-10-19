using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Categories;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;
using Shouldly;

namespace Mita.Api.IntegrationTests.Persistence;

public sealed class LedgerTests(MitaApiFactory factory) : IClassFixture<MitaApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Should_IncludeTransactionLegsInBalance()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId, openingBalance: 10_000);
        var expense = await TestData.AddCategoryAsync(factory, userId);
        var income = await TestData.AddCategoryAsync(factory, userId, CategoryKind.Income);
        await TestData.AddTransactionAsync(factory, account, expense, 1_200);
        await TestData.AddTransactionAsync(factory, account, income, 500);

        using var response = await client.Http.GetAsync("/v1/accounts", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var item = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray()
            .Single(candidate => candidate.GetProperty("id").GetGuid() == account.Id);
        item.GetProperty("balance").GetInt64().ShouldBe(9_300);
        item.GetProperty("inUse").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Should_ResolvePlnValue_LikeRateTable()
    {
        await using var isolated = factory.WithIsolatedData();
        await TestData.AddRatesAsync(
            isolated,
            (Currency.EUR, "2026-03-06", 4.27m),
            (Currency.EUR, "2026-03-09", 4.30m));

        (await PlnValueAsync(isolated, "EUR", new DateOnly(2026, 3, 8))).ShouldBe(4.27m);
        (await PlnValueAsync(isolated, "EUR", new DateOnly(2020, 1, 1))).ShouldBe(4.27m);
        (await PlnValueAsync(isolated, "PLN", new DateOnly(2026, 3, 8))).ShouldBe(1m);
        (await PlnValueAsync(isolated, "USD", new DateOnly(2026, 3, 8))).ShouldBeNull();
    }

    private static async Task<decimal?> PlnValueAsync(
        WebApplicationFactory<Program> app,
        string currency,
        DateOnly date)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Database
            .SqlQuery<decimal?>($"SELECT pln_value({currency}, {date}) AS \"Value\"")
            .SingleAsync(Ct);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
