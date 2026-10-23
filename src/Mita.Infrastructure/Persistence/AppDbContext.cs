using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Mita.Domain.Accounts;
using Mita.Domain.Budgets;
using Mita.Domain.Categories;
using Mita.Domain.Debts;
using Mita.Domain.Goals;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Domain.Tags;
using Mita.Domain.Transactions;
using Mita.Domain.Users;
using Mita.Infrastructure.Authentication;
using Mita.Infrastructure.Identity;
using Mita.Infrastructure.Persistence.ReadModels;

namespace Mita.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<AppUser, Guid>(options)
{
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Transfer> Transfers => Set<Transfer>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    public DbSet<AccountMovement> AccountMovements => Set<AccountMovement>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<Budget> Budgets => Set<Budget>();

    public DbSet<Goal> Goals => Set<Goal>();

    public DbSet<GoalContribution> GoalContributions => Set<GoalContribution>();

    public DbSet<Debt> Debts => Set<Debt>();

    public DbSet<DebtRepayment> DebtRepayments => Set<DebtRepayment>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<TransactionTag> TransactionTags => Set<TransactionTag>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    public DbSet<GroupCategory> GroupCategories => Set<GroupCategory>();

    public DbSet<GroupExpense> GroupExpenses => Set<GroupExpense>();

    public DbSet<GroupExpenseShare> GroupExpenseShares => Set<GroupExpenseShare>();

    public DbSet<GroupRepayment> GroupRepayments => Set<GroupRepayment>();

    public DbSet<GroupEntryEvent> GroupEntryEvents => Set<GroupEntryEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
