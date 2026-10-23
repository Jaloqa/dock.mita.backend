using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.CreateGroupRepayment;

public sealed class CreateGroupRepaymentHandler(
    AppDbContext db,
    GroupAccess access,
    GroupRepaymentWriter writer,
    TimeProvider time)
    : IRequestHandler<CreateGroupRepaymentCommand, Result<GroupRepaymentDto>>
{
    public async Task<Result<GroupRepaymentDto>> Handle(
        CreateGroupRepaymentCommand command,
        CancellationToken cancellationToken)
    {
        var input = GroupRepaymentInputParser.Parse(command);

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

        var repayment = GroupRepayment.Create(group.GroupId, prepared.Value);
        db.GroupRepayments.Add(repayment);
        GroupEntryEvents.Append(
            db,
            group,
            GroupEntryKind.Repayment,
            repayment.Id,
            GroupEntryAction.Created,
            GroupRepaymentState.From(repayment),
            time.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GroupRepaymentQueries.SingleAsync(db, group, repayment.Id, cancellationToken);
    }
}
