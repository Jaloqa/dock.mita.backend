using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.CancelInvite;

public sealed class CancelInviteHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<CancelInviteCommand, Result>
{
    public async Task<Result> Handle(CancelInviteCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var context = await access.OpenOwnedAsync(command.GroupId, GroupLock.Update, cancellationToken);
        if (context is null)
        {
            return Error.NotFound();
        }

        var member = await db.MemberOfGroupAsync(context.GroupId, command.MemberId, cancellationToken);
        if (member is null)
        {
            return Error.NotFound();
        }

        member.CancelInvite();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
