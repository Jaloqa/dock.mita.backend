using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Settings;
using Mita.Domain.Money;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Budgets;

public sealed record BudgetWriteContext(Currency BaseCurrency, MonthStart MonthStart, string? CategoryError);

public static class BudgetWriteLocks
{
    public static async Task<BudgetWriteContext> LockAsync(
        this AppDbContext db,
        Guid userId,
        Guid categoryId,
        CancellationToken ct)
    {
        await db.Database
            .SqlQuery<string>($"""SELECT "BaseCurrency" AS "Value" FROM user_settings WHERE "UserId" = {userId} FOR SHARE""")
            .ToListAsync(ct);
        var (baseCurrency, monthStart) = await db.SettingsAsync(userId, ct);

        var categories = await db.Categories
            .FromSql($"""SELECT * FROM categories WHERE "Id" = {categoryId} AND "UserId" = {userId} FOR SHARE""")
            .AsNoTracking()
            .ToListAsync(ct);
        var category = categories.SingleOrDefault();

        return new BudgetWriteContext(baseCurrency, monthStart, BudgetReferenceRules.CategoryError(category?.Kind, category?.System));
    }
}