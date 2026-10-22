using Microsoft.EntityFrameworkCore;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Transactions;

public static class TransactionBaseAmounts
{
    public static async Task<bool> RecomputeAsync(this AppDbContext db, Guid userId, Currency baseCurrency, CancellationToken ct)
    {
        var target = baseCurrency.ToString();

        var rateMissing = await db.Database
            .SqlQuery<bool>($"""
                SELECT EXISTS (
                    SELECT 1 FROM transactions t
                    JOIN accounts a ON a."Id" = t."AccountId"
                    WHERE t."UserId" = {userId}
                      AND t."Currency" <> {target}
                      AND (pln_value(a."Currency", t."Date") IS NULL OR pln_value({target}, t."Date") IS NULL)) AS "Value"
                """)
            .SingleAsync(ct);
        if (rateMissing)
        {
            return false;
        }

        await db.Database.ExecuteSqlAsync($"""
            UPDATE transactions t
            SET "BaseAmount" = CASE
                WHEN t."Currency" = {target} THEN t."Amount"
                ELSE round(t."AccountAmount" * round_rate(pln_value(a."Currency", t."Date") / pln_value({target}, t."Date"))
                    * power(10::numeric, currency_exponent({target}) - currency_exponent(a."Currency")))
            END
            FROM accounts a
            WHERE a."Id" = t."AccountId" AND t."UserId" = {userId}
            """, ct);
        return true;
    }
}
