using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.RestoreGroupRepayment;

public sealed class RestoreGroupRepaymentHandler(AppDbContext db, GroupAccess access, TimeProvider time)
    : IRequestHandler<RestoreGroupRepaymentCommand, Result<GroupRepaymentDto>>
{
    public async Task<Result<GroupRepaymentDto>> Handle(
        RestoreGroupRepaymentCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var group = await access.OpenAsync(command.Scope, GroupLock.Share, cancellationToken);
        if (group is null)
        {
            return Error.NotFound();
        }

        var repayment = await db.LockRepaymentAsync(group.GroupId, command.RepaymentId, deleted: true, cancellationToken);
        if (repayment is null)
        {
            return Error.NotFound();
        }

        repayment.Restore();
        GroupEntryEvents.Append(
            db,
            group,
            GroupEntryKind.Repayment,
            repayment.Id,
            GroupEntryAction.Restored,
            GroupRepaymentState.From(repayment),
            time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GroupRepaymentQueries.SingleAsync(db, group, repayment.Id, cancellationToken);
    }
}
