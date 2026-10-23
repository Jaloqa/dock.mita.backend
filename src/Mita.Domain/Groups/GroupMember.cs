namespace Mita.Domain.Groups;

public sealed class GroupMember
{
    public const int NameMaxLength = 40;

    public const int EmailMaxLength = 254;

    private GroupMember()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int Position { get; private set; }

    public Guid? UserId { get; private set; }

    public string? InvitedEmail { get; private set; }

    public DateTimeOffset? InvitedAt { get; private set; }

    public bool IsLinked => UserId is not null;

    public static GroupMember Create(Guid groupId, string name, int position) => new()
    {
        Id = Guid.CreateVersion7(),
        GroupId = groupId,
        Name = name,
        Position = position
    };

    public void Rename(string name) => Name = name;

    public void LinkTo(Guid userId)
    {
        if (UserId == userId)
        {
            return;
        }

        if (IsLinked)
        {
            throw new InvalidOperationException("The member is linked to another account.");
        }

        UserId = userId;
        CancelInvite();
    }

    public void Unlink() => UserId = null;

    public void Invite(string email, DateTimeOffset at)
    {
        if (IsLinked)
        {
            throw new InvalidOperationException("A linked member cannot be invited.");
        }

        if (string.IsNullOrWhiteSpace(email)
            || email.Length > EmailMaxLength
            || !string.Equals(email, email.Trim().ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new ArgumentException("The email must be trimmed, lower-case and at most 254 characters.", nameof(email));
        }

        InvitedEmail = email;
        InvitedAt = at;
    }

    public void CancelInvite()
    {
        InvitedEmail = null;
        InvitedAt = null;
    }
}
