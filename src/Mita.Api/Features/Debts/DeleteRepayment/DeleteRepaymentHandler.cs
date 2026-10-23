using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Debts;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Debts.DeleteRepayment;

public sealed class DeleteRepaymentHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteRepaymentCommand, Result>
{
    public async Task<Result> Handle(DeleteRepaymentCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var debt = await db.LockDebtAsync(userId, command.DebtId, cancellationToken);
        if (debt is null)
        {
            return Error.NotFound();
        }

        var repayment = await db.DebtRepayments.SingleOrDefaultAsync(
            candidate => candidate.Id == command.RepaymentId && candidate.DebtId == debt.Id && candidate.UserId == userId,
            cancellationToken);
        if (repayment is null)
        {
            return Error.NotFound();
        }

        debt.Unrepay(repayment.DebtAmount);
        db.DebtRepayments.Remove(repayment);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
