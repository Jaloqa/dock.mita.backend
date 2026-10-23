using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.RestoreExpense;

public sealed class RestoreExpenseHandler(AppDbContext db, GroupAccess access, TimeProvider time)
    : IRequestHandler<RestoreExpenseCommand, Result<GroupExpenseDto>>
{
    public async Task<Result<GroupExpenseDto>> Handle(RestoreExpenseCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var group = await access.OpenAsync(command.Scope, GroupLock.Share, cancellationToken);
        if (group is null)
        {
            return Error.NotFound();
        }

        var expense = await db.LockExpenseAsync(group.GroupId, command.ExpenseId, deleted: true, cancellationToken);
        if (expense is null)
        {
            return Error.NotFound();
        }

        expense.Restore();
        GroupEntryEvents.Append(
            db,
            group,
            GroupEntryKind.Expense,
            expense.Id,
            GroupEntryAction.Restored,
            await GroupExpenseState.CaptureAsync(db, expense, cancellationToken),
            time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await db.SingleAsync(group, expense.Id, cancellationToken);
    }
}
