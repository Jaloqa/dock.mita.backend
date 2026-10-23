using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.GroupInvites.ListGroupInvites;

public sealed class ListGroupInvitesHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListGroupInvitesQuery, Result<PageResponse<GroupInviteDto>>>
{
    public async Task<Result<PageResponse<GroupInviteDto>>> Handle(
        ListGroupInvitesQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var page = new PageRequest(query.Page, query.PageSize);
        if (await db.CurrentAsync(userId, cancellationToken) is not { } email)
        {
            return new PageResponse<GroupInviteDto>([], page.Page, page.PageSize, 0);
        }

        return await db.GroupMembers.AsNoTracking()
            .Where(member => member.InvitedEmail == email
                && member.UserId == null
                && !db.GroupMembers.Any(linked => linked.GroupId == member.GroupId && linked.UserId == userId))
            .Join(db.Groups, member => member.GroupId, group => group.Id, (member, group) => new { member, group })
            .Join(
                db.Users,
                row => row.group.OwnerId,
                owner => owner.Id,
                (row, owner) => new { row.member, row.group, OwnerName = owner.Name })
            .OrderByDescending(row => row.member.InvitedAt)
            .ThenByDescending(row => row.member.Id)
            .Select(row => new GroupInviteDto(
                row.member.Id,
                row.group.Id,
                row.group.Name,
                row.member.Name,
                row.OwnerName,
                row.member.InvitedAt!.Value))
            .ToPageAsync(page, cancellationToken);
    }
}
