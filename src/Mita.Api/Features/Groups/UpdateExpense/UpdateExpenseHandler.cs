using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.UpdateExpense;

public sealed class UpdateExpenseHandler(AppDbContext db, GroupAccess access, GroupExpenseWriter writer, TimeProvider time)
    : IRequestHandler<UpdateExpenseCommand, Result<GroupExpenseDto>>
{
    public async Task<Result<GroupExpenseDto>> Handle(UpdateExpenseCommand command, CancellationToken cancellationToken)
    {
        var input = ExpenseInputParser.Parse(command);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var group = await access.OpenAsync(command.Scope, GroupLock.Share, cancellationToken);
        if (group is null)
        {
            return Error.NotFound();
        }

        var expense = await db.LockExpenseAsync(group.GroupId, command.ExpenseId, deleted: false, cancellationToken);
        if (expense is null)
        {
            return Error.NotFound();
        }

        var prepared = await writer.PrepareAsync(group, input, cancellationToken);
        if (!prepared.IsSuccess)
        {
            return prepared.Error!;
        }

        expense.Update(prepared.Value);
        GroupEntryEvents.Append(
            db,
            group,
            GroupEntryKind.Expense,
            expense.Id,
            GroupEntryAction.Updated,
            await GroupExpenseState.CaptureAsync(db, expense, cancellationToken),
            time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await db.SingleAsync(group, expense.Id, cancellationToken);
    }
}
