using Microsoft.EntityFrameworkCore;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public sealed record OpenShare(
    Guid ExpenseId,
    Guid PayerId,
    DateOnly Date,
    string? Description,
    string CategoryId,
    long Share,
    long Open);

public static class GroupOpenShares
{
    public static async Task<IReadOnlyList<OpenShare>> OpenSharesAsync(
        this AppDbContext db,
        Guid groupId,
        Guid debtorMemberId,
        Guid? creditorMemberId,
        Guid? excludedRepaymentId,
        CancellationToken ct)
    {
        var expenses = db.GroupExpenses.AsNoTracking()
            .Where(expense => expense.GroupId == groupId
                && expense.DeletedAt == null
                && expense.PayerId != debtorMemberId);
        if (creditorMemberId is { } creditor)
        {
            expenses = expenses.Where(expense => expense.PayerId == creditor);
        }

        var covers = db.GroupRepaymentCovers.AsNoTracking()
            .Where(cover => db.GroupRepayments.Any(repayment => repayment.Id == cover.RepaymentId
                && repayment.DeletedAt == null
                && repayment.FromMemberId == debtorMemberId));
        if (excludedRepaymentId is { } excluded)
        {
            covers = covers.Where(cover => cover.RepaymentId != excluded);
        }

        var rows = await expenses
            .Join(
                db.GroupExpenseShares.AsNoTracking().Where(share => share.MemberId == debtorMemberId),
                expense => expense.Id,
                share => share.ExpenseId,
                (expense, share) => new
                {
                    expense.Id,
                    expense.PayerId,
                    expense.Date,
                    expense.CreatedAt,
                    expense.Description,
                    expense.StandardCategory,
                    expense.CategoryId,
                    Share = share.GroupAmount
                })
            .OrderBy(row => row.Date)
            .ThenBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .Select(row => new
            {
                row.Id,
                row.PayerId,
                row.Date,
                row.Description,
                row.StandardCategory,
                row.CategoryId,
                row.Share,
                Covered = covers.Where(cover => cover.ExpenseId == row.Id).Sum(cover => cover.GroupAmount)
            })
            .ToListAsync(ct);

        return
        [
            .. rows
                .Select(row => new OpenShare(
                    row.Id,
                    row.PayerId,
                    row.Date,
                    row.Description,
                    GroupWire.ToWireCategory(row.StandardCategory, row.CategoryId),
                    row.Share,
                    Math.Max(0, row.Share - row.Covered)))
                .Where(share => share.Open > 0)
        ];
    }
}
