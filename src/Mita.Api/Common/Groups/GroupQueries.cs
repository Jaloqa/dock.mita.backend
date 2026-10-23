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

    public static Task<GroupMember?> MemberOfGroupAsync(
        this AppDbContext db,
        Guid groupId,
        Guid memberId,
        CancellationToken ct) =>
        db.GroupMembers.SingleOrDefaultAsync(member => member.Id == memberId && member.GroupId == groupId, ct);

    public static async Task<bool> IsCurrencyLockedAsync(this AppDbContext db, Guid groupId, CancellationToken ct) =>
        await db.GroupExpenses.AnyAsync(expense => expense.GroupId == groupId, ct)
        || await db.GroupRepayments.AnyAsync(repayment => repayment.GroupId == groupId, ct);

    public static IQueryable<GroupSummaryDto> ProjectSummaries(
        this IQueryable<Group> groups,
        AppDbContext db,
        Guid userId)
    {
        var ownerRole = GroupWire.ToWire(GroupRole.Owner);
        var memberRole = GroupWire.ToWire(GroupRole.Member);

        return groups.Select(group => new GroupSummaryDto(
            group.Id,
            group.Name,
            CurrencyWire.ToWire(group.Currency),
            db.GroupMembers.Count(member => member.GroupId == group.Id),
            db.GroupExpenses
                .Where(expense => expense.GroupId == group.Id && expense.DeletedAt == null)
                .Sum(expense => (long?)expense.GroupAmount) ?? 0,
            group.OwnerId == userId ? ownerRole : memberRole));
    }

    public static async Task<IReadOnlyList<GroupMemberDto>> LoadMembersAsync(
        this IQueryable<GroupMember> members,
        AppDbContext db,
        GroupContext group,
        CancellationToken ct)
    {
        var groupId = group.GroupId;
        var ownerView = group.Role == GroupRole.Owner;

        var rows = await members.AsNoTracking()
            .Where(member => member.GroupId == groupId)
            .OrderBy(member => member.Position)
            .Select(member => new
            {
                member.Id,
                member.Name,
                InUse = db.UsedMemberIds(groupId).Contains(member.Id),
                Linked = member.UserId != null,
                LinkedName = db.Users
                    .Where(user => user.Id == member.UserId)
                    .Select(user => user.Name)
                    .FirstOrDefault(),
                member.InvitedEmail
            })
            .ToListAsync(ct);

        return
        [
            .. rows.Select(member => new GroupMemberDto(
                member.Id,
                member.Name,
                member.InUse,
                member.Linked,
                ownerView ? member.LinkedName : null,
                ownerView ? member.InvitedEmail : null))
        ];
    }

    public static async Task<GroupDto> LoadAsync(this AppDbContext db, GroupContext group, CancellationToken ct)
    {
        var groupId = group.GroupId;
        var ownerView = group.Role == GroupRole.Owner;

        var header = await db.Groups.AsNoTracking()
            .Where(row => row.Id == groupId)
            .Select(row => new
            {
                row.Id,
                row.Name,
                row.Currency,
                row.ShareToken,
                Total = db.GroupExpenses
                    .Where(expense => expense.GroupId == row.Id && expense.DeletedAt == null)
                    .Sum(expense => (long?)expense.GroupAmount) ?? 0,
                Locked = db.GroupExpenses.Any(expense => expense.GroupId == row.Id)
                    || db.GroupRepayments.Any(repayment => repayment.GroupId == row.Id)
            })
            .SingleAsync(ct);

        var members = await db.GroupMembers.LoadMembersAsync(db, group, ct);

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
            ownerView ? header.ShareToken : null,
            GroupWire.ToWire(group.Role),
            group.MyMemberId);
    }
}
