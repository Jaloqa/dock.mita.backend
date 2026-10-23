using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.CreateExpense;

public sealed class CreateExpenseHandler(AppDbContext db, GroupAccess access, GroupExpenseWriter writer, TimeProvider time)
    : IRequestHandler<CreateExpenseCommand, Result<GroupExpenseDto>>
{
    public async Task<Result<GroupExpenseDto>> Handle(CreateExpenseCommand command, CancellationToken cancellationToken)
    {
        var input = ExpenseInputParser.Parse(command);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var opened = await access.OpenForWriteAsync(command.Scope, GroupLock.Share, cancellationToken);
        if (!opened.IsSuccess)
        {
            return opened.Error!;
        }

        var group = opened.Value;

        var prepared = await writer.PrepareAsync(group, input, cancellationToken);
        if (!prepared.IsSuccess)
        {
            return prepared.Error!;
        }

        var details = prepared.Value;
        var expense = GroupExpense.Create(group.GroupId, details);
        db.GroupExpenses.Add(expense);
        GroupEntryEvents.Append(
            db,
            group,
            GroupEntryKind.Expense,
            expense.Id,
            GroupEntryAction.Created,
            await GroupExpenseState.CaptureAsync(db, expense, cancellationToken),
            time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await db.SingleAsync(group, expense.Id, cancellationToken);
    }
}
