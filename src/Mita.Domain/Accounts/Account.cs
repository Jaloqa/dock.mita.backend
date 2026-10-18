using Mita.Domain.Common;
using Mita.Domain.Money;

namespace Mita.Domain.Accounts;

public sealed class Account : IAuditable
{
    public const int NameMaxLength = 40;

    private Account()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public AccountType Type { get; private set; }

    public Currency Currency { get; private set; }

    public long OpeningBalance { get; private set; }

    public string? CardLast4 { get; private set; }

    public bool Archived { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static Account Create(
        Guid userId,
        string name,
        AccountType type,
        Currency currency,
        long openingBalance,
        string? cardLast4,
        bool archived)
    {
        var account = new Account { Id = Guid.CreateVersion7(), UserId = userId };
        account.Update(name, type, currency, openingBalance, cardLast4, archived);
        return account;
    }

    public void Update(
        string name,
        AccountType type,
        Currency currency,
        long openingBalance,
        string? cardLast4,
        bool archived)
    {
        Name = name;
        Type = type;
        Currency = currency;
        OpeningBalance = openingBalance;
        CardLast4 = type == AccountType.Card ? cardLast4 : null;
        Archived = archived;
    }
}
