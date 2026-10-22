using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Debts.ListRepayments;

public sealed class ListRepaymentsHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListRepaymentsQuery, Result<PageResponse<RepaymentResponse>>>
{
    public async Task<Result<PageResponse<RepaymentResponse>>> Handle(
        ListRepaymentsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var exists = await db.Debts.AsNoTracking()
            .AnyAsync(debt => debt.Id == query.DebtId && debt.UserId == userId, cancellationToken);
        if (!exists)
        {
            return Error.NotFound();
        }

        return await db.DebtRepayments.AsNoTracking()
            .Where(repayment => repayment.DebtId == query.DebtId && repayment.UserId == userId)
            .OrderByDescending(repayment => repayment.Date)
            .ThenByDescending(repayment => repayment.CreatedAt)
            .ThenByDescending(repayment => repayment.Id)
            .Select(repayment => new RepaymentResponse(
                repayment.Id,
                IsoDate.ToWire(repayment.Date),
                repayment.Amount,
                CurrencyWire.ToWire(repayment.Currency),
                (double)repayment.Rate,
                repayment.DebtAmount,
                repayment.AccountId,
                repayment.AccountAmount,
                repayment.Note))
            .ToPageAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
