namespace Mita.Domain.Groups;

public sealed class GroupConfirmation
{
    private GroupConfirmation()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid GroupId { get; private set; }

    public GroupEntryKind Kind { get; private set; }

    public Guid EntryId { get; private set; }

    public string? HandledTerms { get; private set; }

    public string? ConfirmedTerms { get; private set; }

    public Guid? AccountId { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    public static GroupConfirmation Create(Guid userId, Guid groupId, GroupEntryKind kind, Guid entryId) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        GroupId = groupId,
        Kind = kind,
        EntryId = entryId
    };

    public void Decide(string? terms, DateTimeOffset at)
    {
        HandledTerms = terms;
        DecidedAt = at;
    }

    public void Confirm(string? terms, Guid? accountId, DateTimeOffset at)
    {
        HandledTerms = terms;
        ConfirmedTerms = terms;
        AccountId = accountId;
        DecidedAt = at;
    }
}
