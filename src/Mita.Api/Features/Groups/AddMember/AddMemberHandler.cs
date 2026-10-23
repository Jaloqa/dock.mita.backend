using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.AddMember;

public sealed class AddMemberHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<AddMemberCommand, Result<GroupMemberDto>>
{
    public async Task<Result<GroupMemberDto>> Handle(AddMemberCommand command, CancellationToken cancellationToken)
    {
        var name = GroupNames.Parse(command.Name);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var context = await access.OpenOwnedAsync(command.GroupId, GroupLock.Update, cancellationToken);
        if (context is null)
        {
            return Error.NotFound();
        }

        var existing = await db.GroupMembers.AsNoTracking()
            .Where(member => member.GroupId == context.GroupId)
            .Select(member => new { member.Name, member.Position })
            .ToListAsync(cancellationToken);

        if (existing.Any(member => string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Rejected(GroupMemberNames.MemberNameTaken);
        }

        if (existing.Count >= Group.MaxMembers)
        {
            return Rejected(GroupMemberNames.TooManyMembers);
        }

        var added = GroupMember.Create(
            context.GroupId,
            name,
            existing.Select(member => member.Position).DefaultIfEmpty(-1).Max() + 1);
        db.GroupMembers.Add(added);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GroupMemberDto(added.Id, added.Name, false, false, null, null);
    }

    private static Error Rejected(string code) =>
        Error.Validation(new Dictionary<string, string[]> { ["name"] = [code] });
}
