using Mita.Domain.Common;
using Mita.Domain.Money;

namespace Mita.Domain.Groups;

public sealed class Group : IAuditable
{
    public const int NameMaxLength = 40;

    public const int MaxMembers = 50;

    public const int MaxCategories = 100;

    private Group()
    {
    }

    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Currency Currency { get; private set; }

    public string? ShareToken { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static Group Create(Guid ownerId, string name, Currency currency)
    {
        var group = new Group { Id = Guid.CreateVersion7(), OwnerId = ownerId };
        group.Update(name, currency);
        return group;
    }

    public void Update(string name, Currency currency)
    {
        Name = name;
        Currency = currency;
    }

    public void TurnOnShareLink(string token)
    {
        if (!ShareTokens.IsWellFormed(token))
        {
            throw new ArgumentException("The share token is malformed.", nameof(token));
        }

        ShareToken = token;
    }

    public void TurnOffShareLink() => ShareToken = null;
}
