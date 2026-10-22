using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Debts;
using Mita.Api.Common.Time;
using Mita.Domain.Debts;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Debts.CreateDebt;

public sealed class CreateDebtHandler(AppDbContext db, ICurrentUser currentUser, DebtWriter writer, TimeProvider time)
    : IRequestHandler<CreateDebtCommand, Result<DebtDto>>
{
    public async Task<Result<DebtDto>> Handle(CreateDebtCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var input = DebtInputParser.Parse(command);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var prepared = await writer.PrepareAsync(userId, input, null, cancellationToken);
        if (!prepared.IsSuccess)
        {
            return prepared.Error!;
        }

        var debt = Debt.Create(userId, prepared.Value);
        db.Debts.Add(debt);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await db.Debts.AsNoTracking()
            .Where(candidate => candidate.Id == debt.Id)
            .Project(db, UtcCalendar.Today(time))
            .SingleAsync(cancellationToken);
    }
}
