using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Time;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public sealed class GroupExpenseWriter(AppDbContext db, TimeProvider time)
{
    public const string UnknownCategory = "validation.unknownCategory";

    public async Task<Result<ExpenseDetails>> PrepareAsync(GroupContext group, ParsedExpense input, CancellationToken ct)
    {
        var memberIds = await db.GroupMembers.AsNoTracking()
            .Where(member => member.GroupId == group.GroupId)
            .OrderBy(member => member.Position)
            .Select(member => member.Id)
            .ToListAsync(ct);
        var standardCategory = StandardGroupCategories.IsStandard(input.CategoryId) ? input.CategoryId : null;
        Guid? categoryId = standardCategory is null ? Guid.Parse(input.CategoryId) : null;

        var errors = new Dictionary<string, string[]>();
        if (!memberIds.Contains(input.PayerId))
        {
            errors["payerId"] = [GroupSplitInput.UnknownMember];
        }

        if (categoryId is { } id
            && !await db.GroupCategories.AnyAsync(category => category.Id == id && category.GroupId == group.GroupId, ct))
        {
            errors["categoryId"] = [UnknownCategory];
        }

        if (input.Split is { } split && !split.MemberIds.All(memberIds.Contains))
        {
            errors["split"] = [GroupSplitInput.UnknownMember];
        }

        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        var plnValues = await db.FreshPlnValuesAsync(input.Date, UtcCalendar.Today(time), ct);
        var price = GroupEntryPrice.Compute(input.Amount, input.Currency, group.Currency, input.ManualRate, plnValues);
        if (price is null)
        {
            return Error.Unavailable("rates.unavailable");
        }

        if (price.GroupAmount <= 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["rate"] = ["validation.positiveAmount"] });
        }

        if (price.GroupAmount > MinorUnits.MaxAmount)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["rate"] = ["validation.invalidValue"] });
        }

        return new ExpenseDetails(
            input.PayerId,
            input.Amount,
            input.Currency,
            input.Date,
            standardCategory,
            categoryId,
            input.Tags,
            input.Description,
            price.Rate,
            price.RateManual,
            price.GroupAmount,
            input.Split?.Mode ?? GroupSplitMode.Equal,
            Shares(input, price.GroupAmount, memberIds));
    }

    private static IReadOnlyList<GroupShare> Shares(ParsedExpense input, long groupAmount, List<Guid> memberIds)
    {
        if (input.Split is not { } split)
        {
            return GroupExpenseSplits.Equal(input.Amount, groupAmount, memberIds, input.PayerId);
        }

        if (split.Mode == GroupSplitMode.Equal)
        {
            return GroupExpenseSplits.Equal(input.Amount, groupAmount, [.. memberIds.Where(split.MemberIds.Contains)], input.PayerId);
        }

        var amounts = split.MemberIds.Zip(split.Amounts).ToDictionary(share => share.First, share => share.Second);
        return GroupExpenseSplits.Exact(
            groupAmount,
            [.. memberIds.Where(amounts.ContainsKey).Select(memberId => (memberId, amounts[memberId]))],
            input.PayerId);
    }
}
