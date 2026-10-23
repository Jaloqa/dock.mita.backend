using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Money;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public static class GroupQueries
{
    public static IQueryable<Guid> UsedMemberIds(this AppDbContext db, Guid groupId) =>
        db.GroupEntryEvents
            .Where(entryEvent => entryEvent.GroupId == groupId && entryEvent.ActorMemberId != null)
            .Select(entryEvent => entryEvent.ActorMemberId!.Value)
            .Union(db.Database.SqlQuery<Guid>(
                $"""
                SELECT named.member_id::uuid AS "Value"
                FROM group_entry_events AS entry_event
                CROSS JOIN LATERAL (
                    SELECT entry_event."State" ->> 'payerId'
                    UNION ALL SELECT entry_event."State" ->> 'fromMemberId'
                    UNION ALL SELECT entry_event."State" ->> 'toMemberId'
                    UNION ALL SELECT share ->> 'memberId' FROM jsonb_array_elements(entry_event."State" -> 'shares') AS share
                ) AS named (member_id)
                WHERE entry_event."GroupId" = {groupId} AND named.member_id IS NOT NULL
                """));

    public static async Task<bool> IsCurrencyLockedAsync(this AppDbContext db, Guid groupId, CancellationToken ct) =>
        await db.GroupExpenses.AnyAsync(expense => expense.GroupId == groupId, ct)
        || await db.GroupRepayments.AnyAsync(repayment => repayment.GroupId == groupId, ct);

    public static IQueryable<GroupSummaryDto> ProjectSummaries(this IQueryable<Group> groups, AppDbContext db) =>
        groups.Select(group => new GroupSummaryDto(
            group.Id,
            group.Name,
            CurrencyWire.ToWire(group.Currency),
            db.GroupMembers.Count(member => member.GroupId == group.Id),
            db.GroupExpenses
                .Where(expense => expense.GroupId == group.Id && expense.DeletedAt == null)
                .Sum(expense => (long?)expense.GroupAmount) ?? 0));

    public static async Task<GroupDto> LoadAsync(
        this AppDbContext db,
        Guid groupId,
        bool includeShareToken,
        CancellationToken ct)
    {
        var header = await db.Groups.AsNoTracking()
            .Where(group => group.Id == groupId)
            .Select(group => new
            {
                group.Id,
                group.Name,
                group.Currency,
                group.ShareToken,
                Total = db.GroupExpenses
                    .Where(expense => expense.GroupId == group.Id && expense.DeletedAt == null)
                    .Sum(expense => (long?)expense.GroupAmount) ?? 0,
                Locked = db.GroupExpenses.Any(expense => expense.GroupId == group.Id)
                    || db.GroupRepayments.Any(repayment => repayment.GroupId == group.Id)
            })
            .SingleAsync(ct);

        var members = await db.GroupMembers.AsNoTracking()
            .Where(member => member.GroupId == groupId)
            .OrderBy(member => member.Position)
            .Select(member => new GroupMemberDto(member.Id, member.Name, db.UsedMemberIds(groupId).Contains(member.Id)))
            .ToListAsync(ct);

        var own = await db.GroupCategories.AsNoTracking()
            .Where(category => category.GroupId == groupId)
            .OrderBy(category => category.CreatedAt)
            .ThenBy(category => category.Id)
            .Select(category => new { category.Id, category.Name })
            .ToListAsync(ct);

        var categories = StandardGroupCategories.Keys
            .Select(key => new GroupCategoryDto(key, true, null))
            .Concat(own.Select(category => new GroupCategoryDto(category.Id.ToString(), false, category.Name)))
            .ToList();

        return new GroupDto(
            header.Id,
            header.Name,
            CurrencyWire.ToWire(header.Currency),
            header.Locked,
            header.Total,
            members,
            categories,
            includeShareToken ? header.ShareToken : null);
    }
}
