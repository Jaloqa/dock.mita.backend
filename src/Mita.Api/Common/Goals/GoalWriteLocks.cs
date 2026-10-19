using Microsoft.EntityFrameworkCore;
using Mita.Domain.Accounts;
using Mita.Domain.Goals;
using Mita.Infrastructure.Persistence;
using Npgsql;

namespace Mita.Api.Common.Goals;

public static class GoalWriteLocks
{
    public static async Task<Account?> LockAccountAsync(
        this AppDbContext db,
        Guid userId,
        Guid accountId,
        CancellationToken ct)
    {
        var accounts = await db.Accounts
            .FromSql($"""SELECT * FROM accounts WHERE "Id" = {accountId} AND "UserId" = {userId} FOR SHARE""")
            .AsNoTracking()
            .ToListAsync(ct);
        return accounts.SingleOrDefault();
    }

    public static async Task<Goal?> LockGoalAsync(
        this AppDbContext db,
        Guid userId,
        Guid goalId,
        CancellationToken ct)
    {
        var goals = await db.Goals
            .FromSql($"""SELECT * FROM goals WHERE "Id" = {goalId} AND "UserId" = {userId} FOR UPDATE""")
            .ToListAsync(ct);
        return goals.SingleOrDefault();
    }

    public static Error? FromConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation }
            ? GoalReferenceRules.Account(GoalReferenceRules.UnknownAccount)
            : null;
}
