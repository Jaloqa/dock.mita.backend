using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.RemoveMember;

public sealed class RemoveMemberHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<RemoveMemberCommand, Result>
{
    public async Task<Result> Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
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

        if (await db.UsedMemberIds(context.GroupId).ContainsAsync(member.Id, cancellationToken))
        {
            return Error.Validation(new Dictionary<string, string[]> { ["memberId"] = ["validation.memberInUse"] });
        }

        db.GroupMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
