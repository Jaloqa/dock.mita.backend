using Mita.Domain.Common;

namespace Mita.Domain.Tags;

public sealed class Tag : IAuditable
{
    public const int NameMaxLength = 30;

    public const int MaxPerTransaction = 10;

    private Tag()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static Tag Create(Guid userId, string name) =>
        new() { Id = Guid.CreateVersion7(), UserId = userId, Name = name };

    public void Rename(string name) => Name = name;
}
