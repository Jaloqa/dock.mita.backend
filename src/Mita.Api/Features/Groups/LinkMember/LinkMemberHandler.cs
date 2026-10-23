using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.LinkMember;

public sealed class LinkMemberHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<LinkMemberCommand, Result<GroupMemberDto>>
{
    public async Task<Result<GroupMemberDto>> Handle(LinkMemberCommand command, CancellationToken cancellationToken)
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

        if (member.UserId != context.OwnerId)
        {
            if (member.IsLinked)
            {
                return Rejected(GroupLinkRules.MemberLinked);
            }

            if (context.MyMemberId is not null)
            {
                return Rejected(GroupLinkRules.AlreadyLinked);
            }

            member.LinkTo(context.OwnerId);
            await db.SaveChangesAsync(cancellationToken);
        }

        var linked = await db.MemberDtoAsync(context, member.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return linked;
    }

    private static Error Rejected(string code) =>
        Error.Validation(new Dictionary<string, string[]> { ["memberId"] = [code] });
}
