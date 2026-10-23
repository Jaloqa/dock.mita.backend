using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public static class GroupLinkRules
{
    public const string MemberLinked = "validation.memberLinked";
    public const string AlreadyLinked = "validation.alreadyLinked";
    public const string InviteSelf = "validation.inviteSelf";
    public const string AlreadyInvited = "validation.alreadyInvited";

    public static async Task<GroupMemberDto> MemberDtoAsync(
        this AppDbContext db,
        GroupContext group,
        Guid memberId,
        CancellationToken ct)
    {
        var members = await db.GroupMembers
            .Where(member => member.Id == memberId)
            .LoadMembersAsync(db, group, ct);
        return members.Single();
    }
}
