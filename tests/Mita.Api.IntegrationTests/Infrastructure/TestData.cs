using System.Globalization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Domain.Accounts;
using Mita.Domain.Categories;
using Mita.Domain.Money;
using Mita.Domain.Transactions;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.IntegrationTests.Infrastructure;

public static class TestData
{
    public static async Task<Account> AddAccountAsync(
        WebApplicationFactory<Program> factory,
        Guid userId,
        Currency currency = Currency.PLN,
        long openingBalance = 0,
        bool archived = false)
    {
        var account = Account.Create(
            userId,
            $"Account {Guid.NewGuid():N}"[..20],
            AccountType.Card,
            currency,
            openingBalance,
            null,
            archived);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Accounts.Add(account);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return account;
    }

    public static async Task AddTransferAsync(
        WebApplicationFactory<Program> factory,
        Account from,
        Account to,
        long amount)
    {
        var rate = from.Currency == to.Currency ? 1m : 2m;
        var transfer = Transfer.Create(from.UserId, new DateOnly(2026, 10, 1), from, to, amount, rate, null);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<Category> AddCategoryAsync(
        WebApplicationFactory<Program> factory,
        Guid userId,
        CategoryKind kind = CategoryKind.Expense,
        string? name = null)
    {
        var category = Category.Create(
            userId,
            name ?? $"Category {Guid.NewGuid():N}"[..20],
            kind,
            CategoryColor.Chart1);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Categories.Add(category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return category;
    }

    public static async Task<Transaction> AddTransactionAsync(
        WebApplicationFactory<Program> factory,
        Account account,
        Category category,
        long amount,
        string date = "2026-10-01")
    {
        var transaction = Transaction.Create(
            account.UserId,
            new TransactionDetails(
                category.Kind,
                DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                string.Empty,
                null,
                category.Id,
                account.Id,
                amount,
                account.Currency,
                1m,
                amount,
                amount));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return transaction;
    }

    public static async Task AddRatesAsync(
        WebApplicationFactory<Program> factory,
        params (Currency Currency, string Date, decimal PlnValue)[] rows)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fetchedAt = DateTimeOffset.UtcNow;
        foreach (var (currency, date, plnValue) in rows)
        {
            var parsed = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var existing = await db.ExchangeRates.SingleOrDefaultAsync(
                rate => rate.Currency == currency && rate.Date == parsed,
                TestContext.Current.CancellationToken);
            if (existing is null)
            {
                db.ExchangeRates.Add(new ExchangeRate(currency, parsed, plnValue, fetchedAt));
            }
            else
            {
                existing.PlnValue = plnValue;
                existing.FetchedAt = fetchedAt;
            }
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
