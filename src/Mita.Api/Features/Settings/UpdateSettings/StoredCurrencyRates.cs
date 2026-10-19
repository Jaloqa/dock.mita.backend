using Microsoft.EntityFrameworkCore;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Settings.UpdateSettings;

public static class StoredCurrencyRates
{
    public static async Task<bool> CoverAsync(this AppDbContext db, Guid userId, Currency newBase, CancellationToken ct)
    {
        var target = newBase.ToString();

        var uncovered = await db.Database
            .SqlQuery<bool>($"""
                WITH stored AS (
                    SELECT "Currency" AS currency FROM budgets WHERE "UserId" = {userId}
                    UNION
                    SELECT "Currency" FROM goals WHERE "UserId" = {userId}
                    UNION
                    SELECT "BaseCurrency" FROM goal_contributions WHERE "UserId" = {userId})
                SELECT EXISTS (SELECT 1 FROM stored)
                   AND (pln_value({target}, CURRENT_DATE) IS NULL
                        OR EXISTS (
                            SELECT 1 FROM stored
                            WHERE currency <> {target} AND pln_value(currency, CURRENT_DATE) IS NULL)) AS "Value"
                """)
            .SingleAsync(ct);

        return !uncovered;
    }
}
