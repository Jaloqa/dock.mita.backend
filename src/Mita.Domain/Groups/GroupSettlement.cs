namespace Mita.Domain.Groups;

public static class GroupSettlement
{
    public static IReadOnlyList<GroupTransfer> Suggest(IReadOnlyList<(Guid MemberId, long Balance)> balances)
    {
        if (balances.Aggregate(Int128.Zero, (sum, balance) => sum + balance.Balance) != 0)
        {
            throw new ArgumentException("The balances must sum to zero.", nameof(balances));
        }

        var remaining = balances.Select(balance => balance.Balance).ToArray();
        var transfers = new List<GroupTransfer>();

        for (var debtor = 0; debtor < remaining.Length; debtor++)
        {
            var owed = -remaining[debtor];
            var creditor = owed > 0 ? Array.IndexOf(remaining, owed) : -1;
            if (creditor >= 0)
            {
                transfers.Add(new GroupTransfer(balances[debtor].MemberId, balances[creditor].MemberId, owed));
                remaining[debtor] = 0;
                remaining[creditor] = 0;
            }
        }

        for (var debtor = Largest(remaining, -1); debtor >= 0; debtor = Largest(remaining, -1))
        {
            var creditor = Largest(remaining, 1);
            var amount = Math.Min(-remaining[debtor], remaining[creditor]);
            transfers.Add(new GroupTransfer(balances[debtor].MemberId, balances[creditor].MemberId, amount));
            remaining[debtor] += amount;
            remaining[creditor] -= amount;
        }

        return transfers;
    }

    private static int Largest(long[] remaining, int sign)
    {
        var found = -1;
        for (var index = 0; index < remaining.Length; index++)
        {
            if (remaining[index] * sign > 0 && (found < 0 || remaining[index] * sign > remaining[found] * sign))
            {
                found = index;
            }
        }

        return found;
    }
}
