using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.RenameMember;

public sealed class RenameMemberHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<RenameMemberCommand, Result<GroupMemberDto>>
{
    public async Task<Result<GroupMemberDto>> Handle(RenameMemberCommand command, CancellationToken cancellationToken)
    {
        var name = GroupNames.Parse(command.Name);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var context = await access.OpenOwnedAsync(command.GroupId, GroupLock.Update, cancellationToken);
        if (context is null)
        {
            return Error.NotFound();
        }

        var members = await db.GroupMembers
            .Where(member => member.GroupId == context.GroupId)
            .ToListAsync(cancellationToken);
        var renamed = members.SingleOrDefault(member => member.Id == command.MemberId);
        if (renamed is null)
        {
            return Error.NotFound();
        }

        if (members.Any(member => member.Id != renamed.Id
            && string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Error.Validation(new Dictionary<string, string[]> { ["name"] = [GroupMemberNames.MemberNameTaken] });
        }

        renamed.Rename(name);
        await db.SaveChangesAsync(cancellationToken);
        var inUse = await db.UsedMemberIds(context.GroupId).ContainsAsync(renamed.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GroupMemberDto(renamed.Id, renamed.Name, inUse);
    }
}
