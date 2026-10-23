using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.ListGroups;

public sealed class ListGroupsHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListGroupsQuery, Result<PageResponse<GroupSummaryDto>>>
{
    public async Task<Result<PageResponse<GroupSummaryDto>>> Handle(
        ListGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        return await db.Groups.AsNoTracking()
            .Where(group => group.OwnerId == userId
                || db.GroupMembers.Any(member => member.GroupId == group.Id && member.UserId == userId))
            .OrderByDescending(group => group.CreatedAt)
            .ThenByDescending(group => group.Id)
            .ProjectSummaries(db, userId)
            .ToPageAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
