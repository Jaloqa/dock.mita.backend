using Microsoft.EntityFrameworkCore;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public sealed class GroupActors
{
    private const string OwnerKind = "owner";
    private const string MemberKind = "member";

    private readonly Guid ownerId;
    private readonly string ownerName;
    private readonly Dictionary<Guid, string> memberNames;

    private GroupActors(Guid ownerId, string ownerName, Dictionary<Guid, string> memberNames)
    {
        this.ownerId = ownerId;
        this.ownerName = ownerName;
        this.memberNames = memberNames;
    }

    public static async Task<GroupActors> LoadAsync(AppDbContext db, GroupContext group, CancellationToken ct)
    {
        var ownerName = await db.Users.AsNoTracking()
            .Where(user => user.Id == group.OwnerId)
            .Select(user => user.Name)
            .SingleAsync(ct);
        var memberNames = await db.GroupMembers.AsNoTracking()
            .Where(member => member.GroupId == group.GroupId)
            .ToDictionaryAsync(member => member.Id, member => member.Name, ct);

        return new GroupActors(group.OwnerId, ownerName, memberNames);
    }

    public GroupActorDto Describe(Guid? actorUserId, Guid? actorMemberId) => (actorUserId, actorMemberId) switch
    {
        (null, { } memberId) => new GroupActorDto(MemberKind, memberId, memberNames[memberId]),
        ({ } userId, null) when userId == ownerId => new GroupActorDto(OwnerKind, null, ownerName),
        _ => throw new InvalidOperationException("The event names an actor outside the group.")
    };
}
