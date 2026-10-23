namespace Mita.Domain.Groups;

public sealed record GroupActor(Guid? UserId, Guid? MemberId)
{
    public static GroupActor Owner(Guid userId) => new(userId, null);

    public static GroupActor Member(Guid memberId) => new(null, memberId);
}
