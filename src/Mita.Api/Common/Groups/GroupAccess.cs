using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public sealed class GroupAccess(AppDbContext db, ICurrentUser currentUser)
{
    public Task<GroupContext?> OpenAsync(GroupScope scope, GroupLock mode, CancellationToken ct) =>
        OpenOwnedAsync(scope.GroupId, mode, ct);

    public async Task<GroupContext?> OpenOwnedAsync(Guid groupId, GroupLock mode, CancellationToken ct)
    {
        var userId = currentUser.Id;
        var groups = mode switch
        {
            GroupLock.Share => db.Groups.FromSql(
                $"""SELECT * FROM "groups" WHERE "Id" = {groupId} AND "OwnerId" = {userId} FOR SHARE"""),
            GroupLock.Update => db.Groups.FromSql(
                $"""SELECT * FROM "groups" WHERE "Id" = {groupId} AND "OwnerId" = {userId} FOR UPDATE"""),
            _ => db.Groups.Where(group => group.Id == groupId && group.OwnerId == userId)
        };

        var rows = await groups.AsNoTracking()
            .Select(group => new { group.Id, group.OwnerId, group.Currency })
            .ToListAsync(ct);
        return rows.Count == 0
            ? null
            : new GroupContext(rows[0].Id, rows[0].OwnerId, rows[0].Currency, GroupActor.Owner(userId));
    }
}
