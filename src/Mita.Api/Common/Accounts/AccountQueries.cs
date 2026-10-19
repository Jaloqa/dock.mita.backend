using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Money;
using Mita.Domain.Accounts;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Accounts;

public static class AccountQueries
{
    public static IQueryable<AccountDto> ProjectAccounts(this AppDbContext db, Guid userId) =>
        db.Accounts.AsNoTracking()
            .Where(account => account.UserId == userId)
            .Project(db);

    // EF cannot filter on a DTO built through a constructor, so a single account is filtered before the projection.
    public static IQueryable<AccountDto> ProjectAccount(this AppDbContext db, Guid userId, Guid accountId) =>
        db.Accounts.AsNoTracking()
            .Where(account => account.UserId == userId && account.Id == accountId)
            .Project(db);

    private static IQueryable<AccountDto> Project(this IQueryable<Account> accounts, AppDbContext db) =>
        accounts
            .OrderBy(account => account.CreatedAt)
            .ThenBy(account => account.Id)
            .Select(account => new AccountDto(
                account.Id,
                account.Name,
                AccountWire.ToWire(account.Type),
                CurrencyWire.ToWire(account.Currency),
                account.OpeningBalance,
                account.OpeningBalance
                    + (db.AccountMovements
                        .Where(movement => movement.AccountId == account.Id)
                        .Sum(movement => (long?)movement.Amount) ?? 0),
                account.CardLast4,
                account.Archived,
                db.AccountMovements.Any(movement => movement.AccountId == account.Id),
                db.Goals.Count(goal => goal.AccountId == account.Id)));
}
