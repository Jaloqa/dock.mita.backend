using Microsoft.EntityFrameworkCore;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public static class GroupEntryLocks
{
    public static async Task<GroupExpense?> LockExpenseAsync(
        this AppDbContext db,
        Guid groupId,
        Guid expenseId,
        bool deleted,
        CancellationToken ct)
    {
        var rows = await db.GroupExpenses
            .FromSql(
                $"""SELECT * FROM group_expenses WHERE "Id" = {expenseId} AND "GroupId" = {groupId} AND ("DeletedAt" IS NOT NULL) = {deleted} FOR UPDATE""")
            .ToListAsync(ct);

        var expense = rows.SingleOrDefault();
        if (expense is null)
        {
            return null;
        }

        // Shares joined to the locked row would come from the snapshot taken before the lock wait, so a waiter
        // would see the expense of the last writer with the shares of the one before.
        await db.Entry(expense).Collection(row => row.Shares).LoadAsync(ct);
        return expense;
    }

    public static async Task<GroupRepayment?> LockRepaymentAsync(
        this AppDbContext db,
        Guid groupId,
        Guid repaymentId,
        bool deleted,
        CancellationToken ct)
    {
        var rows = await db.GroupRepayments
            .FromSql(
                $"""SELECT * FROM group_repayments WHERE "Id" = {repaymentId} AND "GroupId" = {groupId} AND ("DeletedAt" IS NOT NULL) = {deleted} FOR UPDATE""")
            .ToListAsync(ct);

        return rows.SingleOrDefault();
    }
}
