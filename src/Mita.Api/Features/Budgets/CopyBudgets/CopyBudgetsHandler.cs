using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Settings;
using Mita.Api.Common.Wire;
using Mita.Domain.Periods;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Budgets.CopyBudgets;

public sealed class CopyBudgetsHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CopyBudgetsCommand, Result<BudgetCopyResponse>>
{
    public async Task<Result<BudgetCopyResponse>> Handle(CopyBudgetsCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        if (!JsonValues.TryGetPeriodKey(command.Period, out var period))
        {
            throw new InvalidOperationException("The copy input reached the handler without passing validation.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var (baseCurrency, monthStart) = await db.LockSettingsAsync(userId, cancellationToken);
        var source = FinancialPeriod.Shift(period, -1, monthStart);
        var target = period.ToString();
        var sourceKey = source.Key.ToString();
        var sourceFrom = source.From;
        var baseName = baseCurrency.ToString();

        await db.Database
            .SqlQuery<Guid>($"""
                SELECT "Id" AS "Value" FROM categories
                WHERE "Id" IN (SELECT "CategoryId" FROM budgets WHERE "UserId" = {userId} AND "Period" = {sourceKey})
                ORDER BY "Id" FOR SHARE
                """)
            .ToListAsync(cancellationToken);

        var copied = await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO budgets ("Id", "UserId", "CategoryId", "Period", "Limit", "Currency", "Position", "CreatedAt", "UpdatedAt")
            SELECT gen_random_uuid(), "UserId", "CategoryId", {target},
                CASE WHEN "Currency" = {baseName} THEN "Limit"
                    ELSE GREATEST(1, round("Limit" * round(pln_value("Currency", {sourceFrom}) / pln_value({baseName}, {sourceFrom}), 6))) END,
                {baseName},
                row_number() OVER (ORDER BY "CreatedAt", "Position", "Id") - 1,
                now(), now()
            FROM budgets
            WHERE "UserId" = {userId} AND "Period" = {sourceKey}
            ON CONFLICT ("UserId", "CategoryId", "Period") DO NOTHING
            """,
            cancellationToken);

        if (copied == 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["period"] = ["validation.nothingToCopy"] });
        }

        await transaction.CommitAsync(cancellationToken);

        return new BudgetCopyResponse(copied);
    }
}
