namespace Mita.Domain.Groups;

public static class GroupExpenseSplits
{
    public static IReadOnlyList<GroupShare> Equal(
        long amount,
        long groupAmount,
        IReadOnlyList<Guid> memberIds,
        Guid payerId)
    {
        var amounts = MinorAllocation.Allocate(amount, memberIds.Select(_ => 1L).ToArray(), Priority(memberIds, payerId));
        var sharing = Enumerable.Range(0, memberIds.Count).Where(index => amounts[index] > 0).ToArray();
        var sharingIds = sharing.Select(index => memberIds[index]).ToArray();
        return Shares(sharingIds, sharing.Select(index => amounts[index]).ToArray(), groupAmount, Priority(sharingIds, payerId));
    }

    public static IReadOnlyList<GroupShare> Exact(
        long groupAmount,
        IReadOnlyList<(Guid MemberId, long Amount)> shares,
        Guid payerId)
    {
        var memberIds = shares.Select(share => share.MemberId).ToArray();
        var amounts = shares.Select(share => share.Amount).ToArray();
        return Shares(memberIds, amounts, groupAmount, Priority(memberIds, payerId));
    }

    private static int[] Priority(IReadOnlyList<Guid> memberIds, Guid payerId)
    {
        var payer = memberIds.ToList().IndexOf(payerId);
        var others = Enumerable.Range(0, memberIds.Count).Where(index => index != payer);
        return (payer < 0 ? others : others.Prepend(payer)).ToArray();
    }

    private static GroupShare[] Shares(IReadOnlyList<Guid> memberIds, long[] amounts, long groupAmount, int[] priority)
    {
        var groupAmounts = MinorAllocation.Allocate(groupAmount, amounts, priority);
        return memberIds.Select((memberId, index) => new GroupShare(memberId, amounts[index], groupAmounts[index])).ToArray();
    }
}
