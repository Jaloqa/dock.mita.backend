namespace Mita.Domain.Groups;

public sealed class GroupRepaymentCover
{
    private GroupRepaymentCover()
    {
    }

    public Guid RepaymentId { get; private set; }

    public Guid ExpenseId { get; private set; }

    public Guid GroupId { get; private set; }

    public int Position { get; private set; }

    public long GroupAmount { get; private set; }

    internal static GroupRepaymentCover Create(Guid repaymentId, Guid groupId, int position, GroupCover cover) => new()
    {
        RepaymentId = repaymentId,
        ExpenseId = cover.ExpenseId,
        GroupId = groupId,
        Position = position,
        GroupAmount = cover.GroupAmount
    };

    internal void Change(int position, long groupAmount)
    {
        Position = position;
        GroupAmount = groupAmount;
    }
}
