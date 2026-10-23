using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.DeleteGroupRepayment;

public sealed class DeleteGroupRepaymentHandler(AppDbContext db, GroupAccess access, TimeProvider time)
    : IRequestHandler<DeleteGroupRepaymentCommand, Result>
{
    public async Task<Result> Handle(DeleteGroupRepaymentCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var group = await access.OpenAsync(command.Scope, GroupLock.Share, cancellationToken);
        if (group is null)
        {
            return Error.NotFound();
        }

        var repayment = await db.LockRepaymentAsync(group.GroupId, command.RepaymentId, deleted: false, cancellationToken);
        if (repayment is null)
        {
            return Error.NotFound();
        }

        var now = time.GetUtcNow();
        repayment.Delete(now);
        GroupEntryEvents.Append(
            db,
            group,
            GroupEntryKind.Repayment,
            repayment.Id,
            GroupEntryAction.Deleted,
            GroupRepaymentState.From(repayment),
            now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
