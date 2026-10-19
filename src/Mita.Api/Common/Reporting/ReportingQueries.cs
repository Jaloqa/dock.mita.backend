using Microsoft.EntityFrameworkCore;
using Mita.Domain.Categories;
using Mita.Domain.Transactions;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Reporting;

public static class ReportingQueries
{
    public static IQueryable<Transaction> CountedTransactions(
        this AppDbContext db,
        Guid userId,
        DateOnly from,
        DateOnly to) =>
        from transaction in db.Transactions.AsNoTracking()
        join category in db.Categories.AsNoTracking() on transaction.CategoryId equals category.Id
        where transaction.UserId == userId
            && transaction.Date >= @from
            && transaction.Date <= to
            && category.System != SystemCategory.Transfer
        select transaction;

    public static async Task<IReadOnlyList<CategorySpending>> SpendingAsync(
        this AppDbContext db,
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var rows = await (
                from transaction in db.CountedTransactions(userId, @from, to)
                where transaction.Kind == CategoryKind.Expense
                join category in db.Categories.AsNoTracking() on transaction.CategoryId equals category.Id
                group transaction by new { category.Id, category.Name, category.Color, category.System }
                into spending
                select new
                {
                    spending.Key.Id,
                    spending.Key.Name,
                    spending.Key.Color,
                    spending.Key.System,
                    Amount = spending.Sum(transaction => transaction.BaseAmount)
                })
            .Where(spending => spending.Amount > 0)
            .ToListAsync(ct);

        return [.. rows.Select(row => new CategorySpending(row.Id, row.Name, row.Color, row.System, row.Amount))];
    }
}
