namespace Mita.Domain.Groups;

public sealed class GroupExpenseShare
{
    private GroupExpenseShare()
    {
    }

    public Guid ExpenseId { get; private set; }

    public Guid GroupId { get; private set; }

    public Guid MemberId { get; private set; }

    public long Amount { get; private set; }

    public long GroupAmount { get; private set; }

    internal static GroupExpenseShare Create(Guid expenseId, Guid groupId, GroupShare share) => new()
    {
        ExpenseId = expenseId,
        GroupId = groupId,
        MemberId = share.MemberId,
        Amount = share.Amount,
        GroupAmount = share.GroupAmount
    };

    internal void Change(long amount, long groupAmount)
    {
        Amount = amount;
        GroupAmount = groupAmount;
    }
}
