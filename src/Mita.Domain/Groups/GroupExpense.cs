using Mita.Domain.Common;
using Mita.Domain.Money;

namespace Mita.Domain.Groups;

public sealed class GroupExpense : IAuditable
{
    public const int DescriptionMaxLength = 100;

    private readonly List<GroupExpenseShare> shares = [];

    private GroupExpense()
    {
    }

    public Guid Id { get; private set; }

    public Guid GroupId { get; private set; }

    public Guid PayerId { get; private set; }

    public long Amount { get; private set; }

    public Currency Currency { get; private set; }

    public DateOnly Date { get; private set; }

    public string? StandardCategory { get; private set; }

    public Guid? CategoryId { get; private set; }

    public IReadOnlyList<string> Tags { get; private set; } = [];

    public string? Description { get; private set; }

    public decimal Rate { get; private set; }

    public bool RateManual { get; private set; }

    public long GroupAmount { get; private set; }

    public GroupSplitMode Split { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public IReadOnlyCollection<GroupExpenseShare> Shares => shares;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static GroupExpense Create(Guid groupId, ExpenseDetails details)
    {
        var expense = new GroupExpense { Id = Guid.CreateVersion7(), GroupId = groupId };
        expense.Update(details);
        return expense;
    }

    public void Update(ExpenseDetails details)
    {
        Validate(details);
        PayerId = details.PayerId;
        Amount = details.Amount;
        Currency = details.Currency;
        Date = details.Date;
        StandardCategory = details.StandardCategory;
        CategoryId = details.CategoryId;
        Tags = [.. details.Tags];
        Description = details.Description;
        Rate = details.Rate;
        RateManual = details.RateManual;
        GroupAmount = details.GroupAmount;
        Split = details.Split;
        ReplaceShares(details.Shares);
    }

    public void Delete(DateTimeOffset at)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("The expense is already deleted.");
        }

        DeletedAt = at;
    }

    public void Restore()
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("The expense is not deleted.");
        }

        DeletedAt = null;
    }

    private static void Validate(ExpenseDetails details)
    {
        if (details.Amount <= 0 || details.GroupAmount <= 0 || details.Rate <= 0)
        {
            throw new ArgumentException("The amount, the group amount and the rate must be positive.", nameof(details));
        }

        if ((details.StandardCategory is null) == (details.CategoryId is null))
        {
            throw new ArgumentException("Exactly one of the standard category and the category must be set.", nameof(details));
        }

        var split = details.Shares;
        if (split.Count == 0 || split.Select(share => share.MemberId).Distinct().Count() != split.Count)
        {
            throw new ArgumentException("The shares must name at least one member, each once.", nameof(details));
        }

        if (split.Any(share => share.Amount <= 0 || share.GroupAmount < 0))
        {
            throw new ArgumentException("A share must be positive and its group amount not negative.", nameof(details));
        }

        if (split.Sum(share => share.Amount) != details.Amount
            || split.Sum(share => share.GroupAmount) != details.GroupAmount)
        {
            throw new ArgumentException("The shares must add up to the amount and to the group amount.", nameof(details));
        }
    }

    private void ReplaceShares(IReadOnlyList<GroupShare> next)
    {
        shares.RemoveAll(share => next.All(row => row.MemberId != share.MemberId));
        foreach (var share in next)
        {
            var existing = shares.Find(row => row.MemberId == share.MemberId);
            if (existing is null)
            {
                shares.Add(GroupExpenseShare.Create(Id, GroupId, share));
            }
            else
            {
                existing.Change(share.Amount, share.GroupAmount);
            }
        }
    }
}
