using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Time;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public sealed class GroupRepaymentWriter(AppDbContext db, TimeProvider time)
{
    public const string UnknownShare = "validation.unknownShare";

    public async Task<Result<GroupRepaymentDetails>> PrepareAsync(
        GroupContext group,
        ParsedGroupRepayment input,
        Guid? repaymentId,
        CancellationToken ct)
    {
        var memberIds = await db.GroupMembers.AsNoTracking()
            .Where(member => member.GroupId == group.GroupId
                && (member.Id == input.FromMemberId || member.Id == input.ToMemberId))
            .Select(member => member.Id)
            .ToListAsync(ct);

        var errors = new Dictionary<string, string[]>();
        if (!memberIds.Contains(input.FromMemberId))
        {
            errors["fromMemberId"] = [GroupSplitInput.UnknownMember];
        }

        if (!memberIds.Contains(input.ToMemberId))
        {
            errors["toMemberId"] = [GroupSplitInput.UnknownMember];
        }

        var open = await db.OpenSharesAsync(group.GroupId, input.FromMemberId, input.ToMemberId, repaymentId, ct);
        var ownCovers = repaymentId is { } id
            ? await db.GroupRepaymentCovers.AsNoTracking()
                .Where(cover => cover.RepaymentId == id)
                .Select(cover => cover.ExpenseId)
                .ToListAsync(ct)
            : [];
        var settles = input.Settles
            .Where(expenseId => open.Any(share => share.ExpenseId == expenseId) || !ownCovers.Contains(expenseId))
            .ToList();
        if (settles.Any(expenseId => open.All(share => share.ExpenseId != expenseId)))
        {
            errors["settles"] = [UnknownShare];
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

        var covers = GroupCoverAllocation.Allocate(
            price.GroupAmount,
            [.. open.Select(share => (share.ExpenseId, share.Open))],
            settles);

        return new GroupRepaymentDetails(
            input.FromMemberId,
            input.ToMemberId,
            input.Amount,
            input.Currency,
            input.Date,
            price.Rate,
            price.RateManual,
            price.GroupAmount,
            input.Note,
            covers);
    }

    public async Task<IReadOnlyList<GroupCover>> RestoredCoversAsync(
        GroupContext group,
        GroupRepayment repayment,
        CancellationToken ct)
    {
        var open = await db.OpenSharesAsync(
            group.GroupId,
            repayment.FromMemberId,
            repayment.ToMemberId,
            repayment.Id,
            ct);
        return GroupCoverAllocation.Reallocate(
            repayment.GroupAmount,
            [.. open.Select(share => (share.ExpenseId, share.Open))],
            [.. repayment.Covers.OrderBy(cover => cover.Position).Select(cover => cover.ExpenseId)]);
    }
}
