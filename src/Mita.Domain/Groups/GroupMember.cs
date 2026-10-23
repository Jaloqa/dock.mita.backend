namespace Mita.Domain.Groups;

public sealed class GroupMember
{
    public const int NameMaxLength = 40;

    private GroupMember()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int Position { get; private set; }

    public static GroupMember Create(Guid groupId, string name, int position) => new()
    {
        Id = Guid.CreateVersion7(),
        GroupId = groupId,
        Name = name,
        Position = position
    };

    public void Rename(string name) => Name = name;
}
