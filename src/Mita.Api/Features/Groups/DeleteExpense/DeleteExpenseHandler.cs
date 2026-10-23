using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.DeleteExpense;

public sealed class DeleteExpenseHandler(AppDbContext db, GroupAccess access, TimeProvider time)
    : IRequestHandler<DeleteExpenseCommand, Result>
{
    public async Task<Result> Handle(DeleteExpenseCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var opened = await access.OpenForWriteAsync(command.Scope, GroupLock.Share, cancellationToken);
        if (!opened.IsSuccess)
        {
            return opened.Error!;
        }

        var group = opened.Value;

        var expense = await db.LockExpenseAsync(group.GroupId, command.ExpenseId, deleted: false, cancellationToken);
        if (expense is null)
        {
            return Error.NotFound();
        }

        var now = time.GetUtcNow();
        expense.Delete(now);
        GroupEntryEvents.Append(
            db,
            group,
            GroupEntryKind.Expense,
            expense.Id,
            GroupEntryAction.Deleted,
            await GroupExpenseState.CaptureAsync(db, expense, cancellationToken),
            now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
