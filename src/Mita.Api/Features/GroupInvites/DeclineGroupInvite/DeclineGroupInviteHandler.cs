using MediatR;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.GroupInvites.DeclineGroupInvite;

public sealed class DeclineGroupInviteHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeclineGroupInviteCommand, Result>
{
    public async Task<Result> Handle(DeclineGroupInviteCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var member = await db.LockInvitationAsync(currentUser.Id, command.MemberId, cancellationToken);
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
