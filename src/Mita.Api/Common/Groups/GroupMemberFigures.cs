using Microsoft.EntityFrameworkCore;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public sealed record GroupMemberFigures(Guid MemberId, long Paid, long Share, long Sent, long Received)
{
    public long Balance => Paid - Share + Sent - Received;
}

public sealed record LinkedGroupBalance(Guid GroupId, string Name, Currency Currency, Guid MemberId, long Balance);

public static class GroupFigureQueries
{
    public static async Task<IReadOnlyDictionary<Guid, GroupMemberFigures>> FiguresAsync(
        this AppDbContext db,
        IReadOnlyCollection<Guid> memberIds,
        CancellationToken ct)
    {
        if (memberIds.Count == 0)
        {
            return new Dictionary<Guid, GroupMemberFigures>();
        }

        var liveExpenses = db.GroupExpenses.AsNoTracking().Where(expense => expense.DeletedAt == null);
        var liveRepayments = db.GroupRepayments.AsNoTracking().Where(repayment => repayment.DeletedAt == null);

        var paid = await liveExpenses
            .Where(expense => memberIds.Contains(expense.PayerId))
            .GroupBy(expense => expense.PayerId)
            .Select(byPayer => new { MemberId = byPayer.Key, Amount = byPayer.Sum(expense => expense.GroupAmount) })
            .ToDictionaryAsync(row => row.MemberId, row => row.Amount, ct);

        var shares = await db.GroupExpenseShares.AsNoTracking()
            .Where(share => memberIds.Contains(share.MemberId))
            .Join(liveExpenses, share => share.ExpenseId, expense => expense.Id, (share, _) => share)
            .GroupBy(share => share.MemberId)
            .Select(byMember => new { MemberId = byMember.Key, Amount = byMember.Sum(share => share.GroupAmount) })
            .ToDictionaryAsync(row => row.MemberId, row => row.Amount, ct);

        var sent = await liveRepayments
            .Where(repayment => memberIds.Contains(repayment.FromMemberId))
            .GroupBy(repayment => repayment.FromMemberId)
            .Select(bySender => new { MemberId = bySender.Key, Amount = bySender.Sum(repayment => repayment.GroupAmount) })
            .ToDictionaryAsync(row => row.MemberId, row => row.Amount, ct);

        var received = await liveRepayments
            .Where(repayment => memberIds.Contains(repayment.ToMemberId))
            .GroupBy(repayment => repayment.ToMemberId)
            .Select(byReceiver => new { MemberId = byReceiver.Key, Amount = byReceiver.Sum(repayment => repayment.GroupAmount) })
            .ToDictionaryAsync(row => row.MemberId, row => row.Amount, ct);

        return memberIds
            .Distinct()
            .ToDictionary(
                memberId => memberId,
                memberId => new GroupMemberFigures(
                    memberId,
                    paid.GetValueOrDefault(memberId),
                    shares.GetValueOrDefault(memberId),
                    sent.GetValueOrDefault(memberId),
                    received.GetValueOrDefault(memberId)));
    }

    public static async Task<IReadOnlyList<LinkedGroupBalance>> LinkedBalancesAsync(
        this AppDbContext db,
        Guid userId,
        CancellationToken ct)
    {
        var links = await db.GroupMembers.AsNoTracking()
            .Where(member => member.UserId == userId)
            .Join(
                db.Groups.AsNoTracking(),
                member => member.GroupId,
                group => group.Id,
                (member, group) => new
                {
                    GroupId = group.Id,
                    group.Name,
                    group.Currency,
                    group.CreatedAt,
                    MemberId = member.Id
                })
            .OrderByDescending(link => link.CreatedAt)
            .ThenByDescending(link => link.GroupId)
            .ToListAsync(ct);

        var figures = await db.FiguresAsync([.. links.Select(link => link.MemberId)], ct);

        return
        [
            .. links.Select(link => new LinkedGroupBalance(
                link.GroupId,
                link.Name,
                link.Currency,
                link.MemberId,
                figures[link.MemberId].Balance))
        ];
    }
}
