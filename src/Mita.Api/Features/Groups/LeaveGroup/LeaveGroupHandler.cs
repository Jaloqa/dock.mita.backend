using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.LeaveGroup;

public sealed class LeaveGroupHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<LeaveGroupCommand, Result>
{
    public async Task<Result> Handle(LeaveGroupCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var context = await access.OpenAsync(GroupScope.User(command.GroupId), GroupLock.Update, cancellationToken);
        if (context?.MyMemberId is not { } memberId)
        {
            return Error.NotFound();
        }

        var member = await db.MemberOfGroupAsync(context.GroupId, memberId, cancellationToken);
        if (member is null)
        {
            return Error.NotFound();
        }

        member.Unlink();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
