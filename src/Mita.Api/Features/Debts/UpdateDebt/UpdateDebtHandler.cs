using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Debts;
using Mita.Api.Common.Time;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Debts.UpdateDebt;

public sealed class UpdateDebtHandler(AppDbContext db, ICurrentUser currentUser, DebtWriter writer, TimeProvider time)
    : IRequestHandler<UpdateDebtCommand, Result<DebtDto>>
{
    public async Task<Result<DebtDto>> Handle(UpdateDebtCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var stored = await db.Debts.AsNoTracking()
            .Where(candidate => candidate.Id == command.Id && candidate.UserId == userId)
            .Select(candidate => new { candidate.AccountId })
            .SingleOrDefaultAsync(cancellationToken);
        if (stored is null)
        {
            return Error.NotFound();
        }

        var input = DebtInputParser.Parse(command);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var prepared = await writer.PrepareAsync(userId, input, stored.AccountId, cancellationToken);
        if (!prepared.IsSuccess)
        {
            return prepared.Error!;
        }

        var debt = await db.LockDebtAsync(userId, command.Id, cancellationToken);
        if (debt is null)
        {
            return Error.NotFound();
        }

        var failures = new Dictionary<string, string[]>();
        if (input.Amount < debt.Repaid)
        {
            failures["amount"] = ["validation.belowRepaid"];
        }

        if (input.Currency != debt.Currency && debt.Repaid > 0)
        {
            failures["currency"] = ["validation.debtCurrencyLocked"];
        }

        if (failures.Count > 0)
        {
            return Error.Validation(failures);
        }

        debt.Update(prepared.Value);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await db.Debts.AsNoTracking()
            .Where(candidate => candidate.Id == debt.Id)
            .Project(db, UtcCalendar.Today(time))
            .SingleAsync(cancellationToken);
    }
}
