namespace Mita.Domain.Groups;

public sealed class GroupEntryEvent
{
    private GroupEntryEvent()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public GroupEntryKind Kind { get; private set; }

    public Guid EntryId { get; private set; }

    public GroupEntryAction Action { get; private set; }

    public DateTimeOffset At { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public Guid? ActorMemberId { get; private set; }

    public string State { get; private set; } = string.Empty;

    public static GroupEntryEvent Create(
        Guid groupId,
        GroupEntryKind kind,
        Guid entryId,
        GroupEntryAction action,
        DateTimeOffset at,
        GroupActor actor,
        string state) => new()
    {
        Id = Guid.CreateVersion7(),
        GroupId = groupId,
        Kind = kind,
        EntryId = entryId,
        Action = action,
        At = at,
        ActorUserId = actor.UserId,
        ActorMemberId = actor.MemberId,
        State = state
    };
}
