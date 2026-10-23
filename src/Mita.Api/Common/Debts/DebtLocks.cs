using Microsoft.EntityFrameworkCore;
using Mita.Domain.Debts;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Debts;

public static class DebtLocks
{
    public static async Task<Debt?> LockDebtAsync(
        this AppDbContext db,
        Guid userId,
        Guid debtId,
        CancellationToken ct)
    {
        var debts = await db.Debts
            .FromSql($"""SELECT * FROM debts WHERE "Id" = {debtId} AND "UserId" = {userId} FOR UPDATE""")
            .ToListAsync(ct);
        return debts.SingleOrDefault();
    }
}
