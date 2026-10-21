using Mita.Domain.Categories;
using Mita.Domain.Common;
using Mita.Domain.Money;
using Mita.Domain.Tags;

namespace Mita.Domain.Transactions;

public sealed class Transaction : IAuditable
{
    public const int TitleMaxLength = 100;

    public const int NoteMaxLength = 200;

    private readonly List<TransactionTag> tags = [];

    private Transaction()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public CategoryKind Kind { get; private set; }

    public DateOnly Date { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Note { get; private set; }

    public Guid CategoryId { get; private set; }

    public Guid AccountId { get; private set; }

    public long Amount { get; private set; }

    public Currency Currency { get; private set; }

    public decimal Rate { get; private set; }

    public long AccountAmount { get; private set; }

    public long BaseAmount { get; private set; }

    public IReadOnlyCollection<TransactionTag> Tags => tags;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static Transaction Create(Guid userId, TransactionDetails details)
    {
        var transaction = new Transaction { Id = Guid.CreateVersion7(), UserId = userId };
        transaction.Update(details);
        return transaction;
    }

    public void Update(TransactionDetails details)
    {
        Kind = details.Kind;
        Date = details.Date;
        Title = details.Title;
        Note = details.Note;
        CategoryId = details.CategoryId;
        AccountId = details.AccountId;
        Amount = details.Amount;
        Currency = details.Currency;
        Rate = details.Rate;
        AccountAmount = details.AccountAmount;
        BaseAmount = details.BaseAmount;
    }
}
