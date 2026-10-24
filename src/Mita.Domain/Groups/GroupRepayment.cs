using Mita.Domain.Common;
using Mita.Domain.Money;

namespace Mita.Domain.Groups;

public sealed class GroupRepayment : IAuditable
{
    public const int NoteMaxLength = 200;

    private readonly List<GroupRepaymentCover> covers = [];

    private GroupRepayment()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public Guid FromMemberId { get; private set; }

    public Guid ToMemberId { get; private set; }

    public long Amount { get; private set; }

    public Currency Currency { get; private set; }

    public DateOnly Date { get; private set; }

    public decimal Rate { get; private set; }

    public bool RateManual { get; private set; }

    public long GroupAmount { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public IReadOnlyCollection<GroupRepaymentCover> Covers => covers;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static GroupRepayment Create(Guid groupId, GroupRepaymentDetails details)
    {
        var repayment = new GroupRepayment { Id = Guid.CreateVersion7(), GroupId = groupId };
        repayment.Update(details);
        return repayment;
    }

    public void Update(GroupRepaymentDetails details)
    {
        Validate(details);
        FromMemberId = details.FromMemberId;
        ToMemberId = details.ToMemberId;
        Amount = details.Amount;
        Currency = details.Currency;
        Date = details.Date;
        Rate = details.Rate;
        RateManual = details.RateManual;
        GroupAmount = details.GroupAmount;
        Note = details.Note;
        ReplaceCovers(details.Covers);
    }

    public void Delete(DateTimeOffset at)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("The repayment is already deleted.");
        }

        DeletedAt = at;
    }

    public void Restore(IReadOnlyList<GroupCover> covers)
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("The repayment is not deleted.");
        }

        ValidateCovers(covers, GroupAmount);
        DeletedAt = null;
        ReplaceCovers(covers);
    }

    private static void Validate(GroupRepaymentDetails details)
    {
        if (details.FromMemberId == details.ToMemberId)
        {
            throw new ArgumentException("A repayment must go between two different members.", nameof(details));
        }

        if (details.Amount <= 0 || details.GroupAmount <= 0 || details.Rate <= 0)
        {
            throw new ArgumentException("The amount, the group amount and the rate must be positive.", nameof(details));
        }

        ValidateCovers(details.Covers, details.GroupAmount);
    }

    private static void ValidateCovers(IReadOnlyList<GroupCover> covers, long groupAmount)
    {
        if (covers.Any(cover => cover.GroupAmount <= 0)
            || covers.Select(cover => cover.ExpenseId).Distinct().Count() != covers.Count)
        {
            throw new ArgumentException("A cover must be positive and name its expense once.", nameof(covers));
        }

        if (covers.Aggregate(Int128.Zero, (sum, cover) => sum + cover.GroupAmount) > groupAmount)
        {
            throw new ArgumentException("The covers must not add up to more than the group amount.", nameof(covers));
        }
    }

    private void ReplaceCovers(IReadOnlyList<GroupCover> next)
    {
        covers.RemoveAll(cover => next.All(row => row.ExpenseId != cover.ExpenseId));
        for (var position = 0; position < next.Count; position++)
        {
            var cover = next[position];
            var existing = covers.Find(row => row.ExpenseId == cover.ExpenseId);
            if (existing is null)
            {
                covers.Add(GroupRepaymentCover.Create(Id, GroupId, position, cover));
            }
            else
            {
                existing.Change(position, cover.GroupAmount);
            }
        }

        covers.Sort((left, right) => left.Position.CompareTo(right.Position));
    }
}
