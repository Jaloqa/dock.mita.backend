using Mita.Domain.Common;

namespace Mita.Domain.Groups;

public sealed class GroupCategory : IAuditable
{
    public const int NameMaxLength = 40;

    private GroupCategory()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static GroupCategory Create(Guid groupId, string name) => new()
    {
        Id = Guid.CreateVersion7(),
        GroupId = groupId,
        Name = name
    };
}
