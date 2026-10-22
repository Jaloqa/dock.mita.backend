using Mita.Domain.Common;
using Mita.Domain.Money;

namespace Mita.Domain.Debts;

public sealed class Debt : IAuditable
{
    public const int CounterpartyMaxLength = 40;

    public const int NoteMaxLength = 200;

    private Debt()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DebtDirection Direction { get; private set; }

    public string Counterparty { get; private set; } = string.Empty;

    public long Amount { get; private set; }

    public Currency Currency { get; private set; }

    public DateOnly Date { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public Guid? AccountId { get; private set; }

    public long? AccountAmount { get; private set; }

    public decimal? Rate { get; private set; }

    public string? Note { get; private set; }

    public long Repaid { get; private set; }

    public long Remaining => Amount - Repaid;

    public DebtStatus Status => Remaining == 0 ? DebtStatus.Settled : DebtStatus.Open;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static Debt Create(Guid userId, DebtDetails details)
    {
        var debt = new Debt { Id = Guid.CreateVersion7(), UserId = userId };
        debt.Update(details);
        return debt;
    }

    public bool IsOverdue(DateOnly today) => Remaining > 0 && DueDate < today;

    public void Update(DebtDetails details)
    {
        Validate(details);
        Direction = details.Direction;
        Counterparty = details.Counterparty;
        Amount = details.Amount;
        Currency = details.Currency;
        Date = details.Date;
        DueDate = details.DueDate;
        AccountId = details.AccountId;
        AccountAmount = details.AccountAmount;
        Rate = details.Rate;
        Note = details.Note;
    }

    public void Repay(long debtAmount)
    {
        if (debtAmount <= 0 || debtAmount > Remaining)
        {
            throw new InvalidOperationException("The repayment must be positive and not exceed the remaining amount.");
        }

        Repaid += debtAmount;
    }

    public void Unrepay(long debtAmount)
    {
        if (debtAmount <= 0 || debtAmount > Repaid)
        {
            throw new InvalidOperationException("The reverted repayment must be positive and not exceed the repaid amount.");
        }

        Repaid -= debtAmount;
    }

    private void Validate(DebtDetails details)
    {
        if (details.Amount <= 0)
        {
            throw new ArgumentException("The amount must be positive.", nameof(details));
        }

        if (details.DueDate < details.Date)
        {
            throw new ArgumentException("The due date must not be before the date.", nameof(details));
        }

        var withAccount = details.AccountId.HasValue;
        if (details.AccountAmount.HasValue != withAccount || details.Rate.HasValue != withAccount)
        {
            throw new ArgumentException("The account, its amount and the rate are set together or not at all.", nameof(details));
        }

        if (details.Amount < Repaid)
        {
            throw new ArgumentException("The amount must not be below the repaid amount.", nameof(details));
        }

        if (Repaid > 0 && details.Currency != Currency)
        {
            throw new ArgumentException("The currency cannot change once a repayment exists.", nameof(details));
        }
    }
}
