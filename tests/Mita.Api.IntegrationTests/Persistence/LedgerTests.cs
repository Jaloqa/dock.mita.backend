using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.Common.Accounts;
using Mita.Api.IntegrationTests.Infrastructure;
using Mita.Domain.Categories;
using Mita.Domain.Debts;
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
    public async Task Should_IncludeDebtLegsInBalance()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId, openingBalance: 10_000);

        var lent = await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 3_000, account: account);
        var afterLending = await AccountAsync(userId, account.Id);
        afterLending.Balance.ShouldBe(7_000);
        afterLending.InUse.ShouldBeTrue();

        await TestData.AddRepaymentAsync(factory, lent, 1_000, account);
        (await AccountAsync(userId, account.Id)).Balance.ShouldBe(8_000);

        var borrowed = await TestData.AddDebtAsync(factory, userId, DebtDirection.Borrowed, 5_000, account: account);
        (await AccountAsync(userId, account.Id)).Balance.ShouldBe(13_000);

        await TestData.AddRepaymentAsync(factory, borrowed, 2_000, account);
        (await AccountAsync(userId, account.Id)).Balance.ShouldBe(11_000);

        var detached = await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 999);
        await TestData.AddRepaymentAsync(factory, detached, 500);
        (await AccountAsync(userId, account.Id)).Balance.ShouldBe(11_000);
    }

    [Fact]
    public async Task Should_DropDebtLegs_When_DebtDeleted()
    {
        using var client = new AuthClient(factory);
        await client.RegisterNewAsync(Ct);
        var userId = await UserIdAsync(client);
        var account = await TestData.AddAccountAsync(factory, userId, openingBalance: 10_000);
        var lent = await TestData.AddDebtAsync(factory, userId, DebtDirection.Lent, 3_000, account: account);
        await TestData.AddRepaymentAsync(factory, lent, 1_000, account);
        (await AccountAsync(userId, account.Id)).Balance.ShouldBe(8_000);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Debts.Remove(await db.Debts.SingleAsync(row => row.Id == lent.Id, Ct));
            await db.SaveChangesAsync(Ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.DebtRepayments.AnyAsync(row => row.DebtId == lent.Id, Ct)).ShouldBeFalse();
            (await db.AccountMovements.AnyAsync(row => row.AccountId == account.Id, Ct)).ShouldBeFalse();
        }

        var afterDeleting = await AccountAsync(userId, account.Id);
        afterDeleting.Balance.ShouldBe(10_000);
        afterDeleting.InUse.ShouldBeFalse();
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

    // The account list endpoint is cached and rows written straight to the database do not invalidate it,
    // so balances are read through the same projection without the cache.
    private async Task<AccountDto> AccountAsync(Guid userId, Guid accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProjectAccount(userId, accountId).SingleAsync(Ct);
    }

    private static async Task<Guid> UserIdAsync(AuthClient client)
    {
        using var response = await client.Http.GetAsync("/v1/me", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }
}
