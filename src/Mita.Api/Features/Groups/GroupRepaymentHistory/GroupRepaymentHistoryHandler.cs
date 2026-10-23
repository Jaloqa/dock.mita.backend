using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Common;
using Mita.Api.Common.Groups;
using Mita.Domain.Groups;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Groups.GroupRepaymentHistory;

public sealed class GroupRepaymentHistoryHandler(AppDbContext db, GroupAccess access)
    : IRequestHandler<GroupRepaymentHistoryQuery, Result<PageResponse<GroupEntryEventDto<GroupRepaymentState>>>>
{
    public async Task<Result<PageResponse<GroupEntryEventDto<GroupRepaymentState>>>> Handle(
        GroupRepaymentHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var group = await access.OpenAsync(query.Scope, GroupLock.None, cancellationToken);
        if (group is null
            || !await db.GroupRepayments.AnyAsync(
                repayment => repayment.Id == query.RepaymentId && repayment.GroupId == group.GroupId,
                cancellationToken))
        {
            return Error.NotFound();
        }

        var rows = await db.GroupEntryEvents.AsNoTracking()
            .Where(entryEvent => entryEvent.GroupId == group.GroupId
                && entryEvent.Kind == GroupEntryKind.Repayment
                && entryEvent.EntryId == query.RepaymentId)
            .OrderByDescending(entryEvent => entryEvent.At)
            .ThenByDescending(entryEvent => entryEvent.Id)
            .Select(entryEvent => new EventRow
            {
                Id = entryEvent.Id,
                Action = entryEvent.Action,
                At = entryEvent.At,
                ActorUserId = entryEvent.ActorUserId,
                ActorMemberId = entryEvent.ActorMemberId,
                State = entryEvent.State
            })
            .ToPageAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);

        var actors = await GroupActors.LoadAsync(db, group, cancellationToken);
        return new PageResponse<GroupEntryEventDto<GroupRepaymentState>>(
            [
                .. rows.Items.Select(row => new GroupEntryEventDto<GroupRepaymentState>(
                    row.Id,
                    GroupWire.ToWire(row.Action),
                    row.At,
                    actors.Describe(row.ActorUserId, row.ActorMemberId),
                    GroupEntryEvents.ReadState<GroupRepaymentState>(row.State)))
            ],
            rows.Page,
            rows.PageSize,
            rows.Total);
    }

    private sealed class EventRow
    {
        public Guid Id { get; init; }

        public GroupEntryAction Action { get; init; }

        public DateTimeOffset At { get; init; }

        public Guid? ActorUserId { get; init; }

        public Guid? ActorMemberId { get; init; }

        public string State { get; init; } = string.Empty;
    }
}
