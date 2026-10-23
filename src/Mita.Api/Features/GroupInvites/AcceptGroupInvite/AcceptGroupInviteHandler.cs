using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.GroupInvites.AcceptGroupInvite;

public sealed class AcceptGroupInviteHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<AcceptGroupInviteCommand, Result<GroupSummaryDto>>
{
    public async Task<Result<GroupSummaryDto>> Handle(
        AcceptGroupInviteCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var member = await db.LockInvitationAsync(userId, command.MemberId, cancellationToken);
        if (member is null)
        {
            return Error.NotFound();
        }

        if (await db.GroupMembers.AnyAsync(
                other => other.GroupId == member.GroupId && other.UserId == userId,
                cancellationToken))
        {
            return Error.Validation(new Dictionary<string, string[]> { ["memberId"] = [GroupLinkRules.AlreadyLinked] });
        }

        member.LinkTo(userId);
        await db.SaveChangesAsync(cancellationToken);
        var summary = await db.Groups.AsNoTracking()
            .Where(group => group.Id == member.GroupId)
            .ProjectSummaries(db, userId)
            .SingleAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return summary;
    }
}
