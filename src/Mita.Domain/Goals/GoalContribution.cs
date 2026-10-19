using Mita.Domain.Common;
using Mita.Domain.Money;

namespace Mita.Domain.Goals;

public sealed class GoalContribution : IAuditable
{
    private GoalContribution()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid GoalId { get; private set; }

    public DateOnly Date { get; private set; }

    public long Amount { get; private set; }

    public Currency Currency { get; private set; }

    public Guid? FromAccountId { get; private set; }

    public Guid? TransferId { get; private set; }

    public long BaseAmount { get; private set; }

    public Currency BaseCurrency { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static GoalContribution Create(
        Guid userId,
        Guid goalId,
        DateOnly date,
        long amount,
        Currency currency,
        Guid? fromAccountId,
        Guid? transferId,
        long baseAmount,
        Currency baseCurrency) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            GoalId = goalId,
            Date = date,
            Amount = amount,
            Currency = currency,
            FromAccountId = fromAccountId,
            TransferId = transferId,
            BaseAmount = baseAmount,
            BaseCurrency = baseCurrency
        };
}
