using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mita.Api.Common.Groups;
using Mita.Domain.Accounts;
using Mita.Domain.Budgets;
using Mita.Domain.Categories;
using Mita.Domain.Debts;
using Mita.Domain.Goals;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Domain.Periods;
using Mita.Domain.Tags;
using Mita.Domain.Transactions;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.IntegrationTests.Infrastructure;

public static class TestData
{
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);

    public static async Task<Account> AddAccountAsync(
        WebApplicationFactory<Program> factory,
        Guid userId,
        Currency currency = Currency.PLN,
        long openingBalance = 0,
        bool archived = false,
        AccountType type = AccountType.Card)
    {
        var account = Account.Create(
            userId,
            $"Account {Guid.NewGuid():N}"[..20],
            type,
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
        string date = "2026-10-01",
        string title = "")
    {
        var transaction = Transaction.Create(
            account.UserId,
            new TransactionDetails(
                category.Kind,
                DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                title,
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

    public static async Task<Tag> AddTagAsync(WebApplicationFactory<Program> factory, Guid userId, string name)
    {
        var tag = Tag.Create(userId, name);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Tags.Add(tag);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return tag;
    }

    public static async Task TagTransactionAsync(
        WebApplicationFactory<Program> factory,
        Transaction transaction,
        params Tag[] tags)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TransactionTags.AddRange(tags.Select(tag => TransactionTag.Create(transaction.Id, tag.Id)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<Budget> AddBudgetAsync(
        WebApplicationFactory<Program> factory,
        Guid userId,
        Guid categoryId,
        string period,
        long limit,
        Currency currency = Currency.PLN)
    {
        var budget = Budget.Create(userId, categoryId, ParsePeriod(period), limit, currency);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return budget;
    }

    public static async Task<Goal> AddGoalAsync(
        WebApplicationFactory<Program> factory,
        Guid userId,
        string name,
        long target,
        long saved = 0,
        Currency currency = Currency.PLN,
        string? deadline = null,
        Guid? accountId = null)
    {
        var goal = Goal.Create(
            userId,
            name,
            target,
            deadline is null ? null : ParsePeriod(deadline),
            accountId,
            currency);
        goal.AddSaved(saved);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Goals.Add(goal);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return goal;
    }

    public static async Task AddContributionAsync(
        WebApplicationFactory<Program> factory,
        Goal goal,
        string date,
        long baseAmount,
        Currency baseCurrency = Currency.PLN)
    {
        var contribution = GoalContribution.Create(
            goal.UserId,
            goal.Id,
            DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            baseAmount,
            baseCurrency,
            null,
            null,
            baseAmount,
            baseCurrency);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.GoalContributions.Add(contribution);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<Debt> AddDebtAsync(
        WebApplicationFactory<Program> factory,
        Guid userId,
        DebtDirection direction,
        long amount,
        Currency currency = Currency.PLN,
        Account? account = null,
        string date = "2026-10-01",
        string? dueDate = null)
    {
        var rate = account is null ? (decimal?)null : account.Currency == currency ? 1m : 2m;
        var debt = Debt.Create(
            userId,
            new DebtDetails(
                direction,
                "Max",
                amount,
                currency,
                ParseDate(date),
                dueDate is null ? null : ParseDate(dueDate),
                account?.Id,
                account is null ? null : MinorUnits.Convert(amount, rate!.Value, currency, account.Currency),
                rate,
                null));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Debts.Add(debt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return debt;
    }

    public static async Task<DebtRepayment> AddRepaymentAsync(
        WebApplicationFactory<Program> factory,
        Debt debt,
        long amount,
        Account? account = null,
        string date = "2026-10-05")
    {
        var accountRate = account is null ? (decimal?)null : account.Currency == debt.Currency ? 1m : 2m;
        var repayment = DebtRepayment.Create(
            debt.UserId,
            debt.Id,
            new RepaymentDetails(
                ParseDate(date),
                amount,
                debt.Currency,
                1m,
                amount,
                account?.Id,
                account is null ? null : MinorUnits.Convert(amount, accountRate!.Value, debt.Currency, account.Currency),
                null));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Debts.SingleAsync(row => row.Id == debt.Id, TestContext.Current.CancellationToken);
        stored.Repay(amount);
        db.DebtRepayments.Add(repayment);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return repayment;
    }

    public static async Task<(Group Group, GroupMember[] Members)> AddGroupAsync(
        WebApplicationFactory<Program> factory,
        Guid ownerId,
        string name = "Trip",
        Currency currency = Currency.EUR,
        params string[] members)
    {
        var group = Group.Create(ownerId, name, currency);
        var rows = (members.Length == 0 ? ["Anna", "Olya", "Max"] : members)
            .Select((member, position) => GroupMember.Create(group.Id, member, position))
            .ToArray();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Groups.Add(group);
        db.GroupMembers.AddRange(rows);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (group, rows);
    }

    public static Task LinkMemberAsync(WebApplicationFactory<Program> factory, GroupMember member, Guid userId) =>
        ChangeMemberAsync(factory, member, (stored, _) => stored.LinkTo(userId));

    public static Task UnlinkMemberAsync(WebApplicationFactory<Program> factory, GroupMember member) =>
        ChangeMemberAsync(factory, member, (stored, _) => stored.Unlink());

    public static Task InviteMemberAsync(WebApplicationFactory<Program> factory, GroupMember member, string email) =>
        ChangeMemberAsync(
            factory,
            member,
            (stored, clock) => stored.Invite(GroupInviteEmails.Normalize(email), clock.GetUtcNow()));

    public static async Task<GroupExpense> AddGroupExpenseAsync(
        WebApplicationFactory<Program> factory,
        Group group,
        GroupMember payer,
        long amount,
        GroupMember[] between,
        string date = "2026-10-01",
        bool deleted = false)
    {
        var shares = GroupExpenseSplits.Equal(amount, amount, between.Select(member => member.Id).ToList(), payer.Id);
        var expense = GroupExpense.Create(
            group.Id,
            new ExpenseDetails(
                payer.Id,
                amount,
                group.Currency,
                ParseDate(date),
                StandardGroupCategories.Default,
                null,
                [],
                null,
                1m,
                false,
                amount,
                GroupSplitMode.Equal,
                shares));
        var state = JsonSerializer.Serialize(
            GroupExpenseState.From(expense, await MemberOrderAsync(factory, group.Id)),
            WireJson);

        await AddEntryAsync(
            factory,
            group,
            GroupEntryKind.Expense,
            expense.Id,
            state,
            deleted ? expense.Delete : null,
            db => db.GroupExpenses.Add(expense));
        return expense;
    }

    public static async Task<GroupRepayment> AddGroupRepaymentAsync(
        WebApplicationFactory<Program> factory,
        Group group,
        GroupMember from,
        GroupMember to,
        long amount,
        string date = "2026-10-05",
        bool deleted = false)
    {
        var repayment = GroupRepayment.Create(
            group.Id,
            new GroupRepaymentDetails(from.Id, to.Id, amount, group.Currency, ParseDate(date), 1m, false, amount, null));
        var state = JsonSerializer.Serialize(GroupRepaymentState.From(repayment), WireJson);

        await AddEntryAsync(
            factory,
            group,
            GroupEntryKind.Repayment,
            repayment.Id,
            state,
            deleted ? repayment.Delete : null,
            db => db.GroupRepayments.Add(repayment));
        return repayment;
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

    private static async Task AddEntryAsync(
        WebApplicationFactory<Program> factory,
        Group group,
        GroupEntryKind kind,
        Guid entryId,
        string state,
        Action<DateTimeOffset>? delete,
        Action<AppDbContext> add)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var owner = GroupActor.Owner(group.OwnerId);
        add(db);
        db.GroupEntryEvents.Add(
            GroupEntryEvent.Create(group.Id, kind, entryId, GroupEntryAction.Created, clock.GetUtcNow(), owner, state));
        if (delete is not null)
        {
            var at = clock.GetUtcNow();
            delete(at);
            db.GroupEntryEvents.Add(
                GroupEntryEvent.Create(group.Id, kind, entryId, GroupEntryAction.Deleted, at, owner, state));
        }

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ChangeMemberAsync(
        WebApplicationFactory<Program> factory,
        GroupMember member,
        Action<GroupMember, TimeProvider> change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.GroupMembers.SingleAsync(
            row => row.Id == member.Id,
            TestContext.Current.CancellationToken);
        change(stored, scope.ServiceProvider.GetRequiredService<TimeProvider>());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid[]> MemberOrderAsync(WebApplicationFactory<Program> factory, Guid groupId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.GroupMembers.AsNoTracking()
            .Where(member => member.GroupId == groupId)
            .OrderBy(member => member.Position)
            .Select(member => member.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
    }

    private static DateOnly ParseDate(string date) =>
        DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static PeriodKey ParsePeriod(string period) =>
        PeriodKey.TryParse(period, out var key) ? key : throw new ArgumentException("Invalid period.", nameof(period));
}
