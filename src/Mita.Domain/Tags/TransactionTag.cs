namespace Mita.Domain.Tags;

public sealed class TransactionTag
{
    private TransactionTag()
    {
    }

    public Guid TransactionId { get; private set; }

    public Guid TagId { get; private set; }

    public Tag Tag { get; private set; } = null!;

    public static TransactionTag Create(Guid transactionId, Guid tagId) =>
        new() { TransactionId = transactionId, TagId = tagId };
}
