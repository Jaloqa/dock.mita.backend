namespace Mita.Domain.Groups;

public static class GroupCoverAllocation
{
    public static IReadOnlyList<GroupCover> Allocate(
        long amount,
        IReadOnlyList<(Guid ExpenseId, long Open)> shares,
        IReadOnlyList<Guid> requested)
    {
        var first = requested.ToHashSet();
        return Take(
            amount,
            shares.Where(share => first.Contains(share.ExpenseId))
                .Concat(shares.Where(share => !first.Contains(share.ExpenseId))));
    }

    public static IReadOnlyList<GroupCover> Reallocate(
        long amount,
        IReadOnlyList<(Guid ExpenseId, long Open)> shares,
        IReadOnlyList<Guid> previous)
    {
        var open = shares.ToDictionary(share => share.ExpenseId, share => share.Open);
        var first = previous.ToHashSet();
        return Take(
            amount,
            previous.Where(open.ContainsKey)
                .Select(expenseId => (expenseId, open[expenseId]))
                .Concat(shares.Where(share => !first.Contains(share.ExpenseId))));
    }

    private static List<GroupCover> Take(long amount, IEnumerable<(Guid ExpenseId, long Open)> ordered)
    {
        var covers = new List<GroupCover>();
        var left = amount;
        foreach (var (expenseId, open) in ordered)
        {
            var cover = Math.Min(left, open);
            if (cover > 0)
            {
                covers.Add(new GroupCover(expenseId, cover));
                left -= cover;
            }
        }

        return covers;
    }
}
