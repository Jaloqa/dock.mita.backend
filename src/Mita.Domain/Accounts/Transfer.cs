using Mita.Domain.Common;
using Mita.Domain.Money;

namespace Mita.Domain.Accounts;

public sealed class Transfer : IAuditable
{
    public const int NoteMaxLength = 200;

    private Transfer()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateOnly Date { get; private set; }

    public Guid FromAccountId { get; private set; }

    public Guid ToAccountId { get; private set; }

    public long FromAmount { get; private set; }

    public long ToAmount { get; private set; }

    public decimal Rate { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static Transfer Create(
        Guid userId,
        DateOnly date,
        Account from,
        Account to,
        long fromAmount,
        decimal rate,
        string? note) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        Date = date,
        FromAccountId = from.Id,
        ToAccountId = to.Id,
        FromAmount = fromAmount,
        ToAmount = MinorUnits.Convert(fromAmount, rate),
        Rate = rate,
        Note = note
    };
}
