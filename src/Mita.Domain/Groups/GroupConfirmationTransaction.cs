namespace Mita.Domain.Groups;

public sealed class GroupConfirmationTransaction
{
    private GroupConfirmationTransaction()
    {
    }

    public Guid TransactionId { get; private set; }

    public Guid ConfirmationId { get; private set; }

    public Guid ExpenseId { get; private set; }

    public static GroupConfirmationTransaction Create(Guid transactionId, Guid confirmationId, Guid expenseId) => new()
    {
        TransactionId = transactionId,
        ConfirmationId = confirmationId,
        ExpenseId = expenseId
    };
}
