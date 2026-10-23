using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.LinkThroughShare;

public sealed class LinkThroughShareHandler(AppDbContext db, GroupAccess access, ICurrentUser currentUser)
    : IRequestHandler<LinkThroughShareCommand, Result<GroupSummaryDto>>
{
    public async Task<Result<GroupSummaryDto>> Handle(
        LinkThroughShareCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var opened = await access.OpenForWriteAsync(command.Scope, GroupLock.Update, cancellationToken);
        if (!opened.IsSuccess)
        {
            return opened.Error!;
        }

        var context = opened.Value;
        var memberId = context.Actor.MemberId
            ?? throw new InvalidOperationException("A link write acts as the member from the header.");
        var member = await db.MemberOfGroupAsync(context.GroupId, memberId, cancellationToken)
            ?? throw new InvalidOperationException("The header member was checked under the group lock.");
        var userId = currentUser.Id;

        if (member.UserId != userId)
        {
            if (member.IsLinked)
            {
                return Rejected(GroupLinkRules.MemberLinked);
            }

            if (await db.GroupMembers.AnyAsync(
                    other => other.GroupId == context.GroupId && other.UserId == userId,
                    cancellationToken))
            {
                return Rejected(GroupLinkRules.AlreadyLinked);
            }

            member.LinkTo(userId);
            await db.SaveChangesAsync(cancellationToken);
        }

        var summary = await db.Groups.AsNoTracking()
            .Where(group => group.Id == context.GroupId)
            .ProjectSummaries(db, userId)
            .SingleAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return summary;
    }

    private static Error Rejected(string code) =>
        Error.Validation(new Dictionary<string, string[]> { ["memberId"] = [code] });
}
