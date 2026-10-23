using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public sealed class GroupAccess(AppDbContext db, ICurrentUser currentUser)
{
    public const string MemberHeader = "X-Group-Member";

    private static readonly GroupActor LinkReader = new(null, null);

    public Task<GroupContext?> OpenAsync(GroupScope scope, GroupLock mode, CancellationToken ct) => scope switch
    {
        OwnerGroupScope owner => OpenOwnedAsync(owner.GroupId, mode, ct),
        LinkGroupScope link => OpenLinkedAsync(link.Token, mode, ct),
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown group scope.")
    };

    public async Task<Result<GroupContext>> OpenForWriteAsync(GroupScope scope, GroupLock mode, CancellationToken ct)
    {
        var context = await OpenAsync(scope, mode, ct);
        if (context is null)
        {
            return Error.NotFound();
        }

        if (scope is not LinkGroupScope link)
        {
            return context;
        }

        if (!Guid.TryParse(link.MemberHeader, out var memberId)
            || !await db.GroupMembers.AnyAsync(
                member => member.Id == memberId && member.GroupId == context.GroupId,
                ct))
        {
            return GroupErrors.MemberRequired;
        }

        return context with { Actor = GroupActor.Member(memberId) };
    }

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

        var row = await HeaderAsync(groups, ct);
        return row is null ? null : new GroupContext(row.Id, row.OwnerId, row.Currency, GroupActor.Owner(userId));
    }

    private async Task<GroupContext?> OpenLinkedAsync(string token, GroupLock mode, CancellationToken ct)
    {
        var groups = mode switch
        {
            GroupLock.Share => db.Groups.FromSql(
                $"""SELECT * FROM "groups" WHERE "ShareToken" = {token} FOR SHARE"""),
            GroupLock.Update => db.Groups.FromSql(
                $"""SELECT * FROM "groups" WHERE "ShareToken" = {token} FOR UPDATE"""),
            _ => db.Groups.Where(group => group.ShareToken == token)
        };

        var row = await HeaderAsync(groups, ct);
        return row is null ? null : new GroupContext(row.Id, row.OwnerId, row.Currency, LinkReader);
    }

    private static async Task<GroupHeader?> HeaderAsync(IQueryable<Group> groups, CancellationToken ct)
    {
        var rows = await groups.AsNoTracking()
            .Select(group => new GroupHeader(group.Id, group.OwnerId, group.Currency))
            .ToListAsync(ct);
        return rows.Count == 0 ? null : rows[0];
    }

    private sealed record GroupHeader(Guid Id, Guid OwnerId, Currency Currency);
}
