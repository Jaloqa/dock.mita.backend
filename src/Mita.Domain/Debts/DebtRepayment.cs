using Mita.Domain.Common;
using Mita.Domain.Money;

namespace Mita.Domain.Debts;

public sealed class DebtRepayment : IAuditable
{
    private DebtRepayment()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid DebtId { get; private set; }

    public DateOnly Date { get; private set; }

    public long Amount { get; private set; }

    public Currency Currency { get; private set; }

    public decimal Rate { get; private set; }

    public long DebtAmount { get; private set; }

    public Guid? AccountId { get; private set; }

    public long? AccountAmount { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static DebtRepayment Create(Guid userId, Guid debtId, RepaymentDetails details) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        DebtId = debtId,
        Date = details.Date,
        Amount = details.Amount,
        Currency = details.Currency,
        Rate = details.Rate,
        DebtAmount = details.DebtAmount,
        AccountId = details.AccountId,
        AccountAmount = details.AccountAmount,
        Note = details.Note
    };
}
