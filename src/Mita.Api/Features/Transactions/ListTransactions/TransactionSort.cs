namespace Mita.Api.Features.Transactions.ListTransactions;

public enum TransactionSortOrder
{
    NewestFirst,
    OldestFirst,
    LargestFirst,
    SmallestFirst
}

public static class TransactionSort
{
    public static bool TryParse(string? value, out TransactionSortOrder order)
    {
        (order, var ok) = value switch
        {
            null or "-date" => (TransactionSortOrder.NewestFirst, true),
            "date" => (TransactionSortOrder.OldestFirst, true),
            "-amount" => (TransactionSortOrder.LargestFirst, true),
            "amount" => (TransactionSortOrder.SmallestFirst, true),
            _ => (default(TransactionSortOrder), false)
        };
        return ok;
    }
}
