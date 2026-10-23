using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public static class GroupRepaymentQueries
{
    public static async Task<PageResponse<GroupRepaymentDto>> PageAsync(
        AppDbContext db,
        GroupContext group,
        bool deleted,
        PageRequest page,
        CancellationToken ct)
    {
        var repayments = db.GroupRepayments.AsNoTracking().Where(repayment => repayment.GroupId == group.GroupId);
        var listed = deleted
            ? repayments.Where(repayment => repayment.DeletedAt != null)
            : repayments.Where(repayment => repayment.DeletedAt == null);
        var rows = await listed
            .OrderByDescending(repayment => repayment.Date)
            .ThenByDescending(repayment => repayment.CreatedAt)
            .ThenByDescending(repayment => repayment.Id)
            .Project(db, group.GroupId)
            .ToPageAsync(page, ct);

        var actors = await GroupActors.LoadAsync(db, group, ct);
        return new PageResponse<GroupRepaymentDto>(
            [.. rows.Items.Select(row => ToDto(row, actors))],
            rows.Page,
            rows.PageSize,
            rows.Total);
    }

    public static async Task<GroupRepaymentDto> SingleAsync(
        AppDbContext db,
        GroupContext group,
        Guid repaymentId,
        CancellationToken ct)
    {
        var row = await db.GroupRepayments.AsNoTracking()
            .Where(repayment => repayment.Id == repaymentId && repayment.GroupId == group.GroupId)
            .Project(db, group.GroupId)
            .SingleAsync(ct);

        return ToDto(row, await GroupActors.LoadAsync(db, group, ct));
    }

    private static IQueryable<RepaymentRow> Project(this IQueryable<GroupRepayment> repayments, AppDbContext db, Guid groupId)
    {
        var events = db.GroupEntryEvents
            .Where(entryEvent => entryEvent.GroupId == groupId && entryEvent.Kind == GroupEntryKind.Repayment);

        return repayments.Select(repayment => new RepaymentRow
        {
            Id = repayment.Id,
            FromMemberId = repayment.FromMemberId,
            ToMemberId = repayment.ToMemberId,
            Amount = repayment.Amount,
            Currency = repayment.Currency,
            Date = repayment.Date,
            Rate = repayment.Rate,
            RateManual = repayment.RateManual,
            GroupAmount = repayment.GroupAmount,
            Note = repayment.Note,
            Deleted = repayment.DeletedAt != null,
            Created = events
                .Where(entryEvent => entryEvent.EntryId == repayment.Id)
                .OrderBy(entryEvent => entryEvent.At)
                .ThenBy(entryEvent => entryEvent.Id)
                .Select(entryEvent => new EventRow
                {
                    Id = entryEvent.Id,
                    At = entryEvent.At,
                    ActorUserId = entryEvent.ActorUserId,
                    ActorMemberId = entryEvent.ActorMemberId
                })
                .FirstOrDefault(),
            Latest = events
                .Where(entryEvent => entryEvent.EntryId == repayment.Id)
                .OrderByDescending(entryEvent => entryEvent.At)
                .ThenByDescending(entryEvent => entryEvent.Id)
                .Select(entryEvent => new EventRow
                {
                    Id = entryEvent.Id,
                    At = entryEvent.At,
                    ActorUserId = entryEvent.ActorUserId,
                    ActorMemberId = entryEvent.ActorMemberId
                })
                .FirstOrDefault()
        });
    }

    private static GroupRepaymentDto ToDto(RepaymentRow row, GroupActors actors)
    {
        var created = row.Created ?? throw new InvalidOperationException("Every repayment has a creation event.");
        var updated = row.Latest is { } latest && latest.Id != created.Id ? latest : null;

        return new GroupRepaymentDto(
            row.Id,
            row.FromMemberId,
            row.ToMemberId,
            row.Amount,
            CurrencyWire.ToWire(row.Currency),
            IsoDate.ToWire(row.Date),
            (double)row.Rate,
            row.RateManual,
            row.GroupAmount,
            row.Note,
            row.Deleted,
            actors.Describe(created.ActorUserId, created.ActorMemberId),
            created.At,
            updated is null ? null : actors.Describe(updated.ActorUserId, updated.ActorMemberId),
            updated?.At);
    }

    private sealed class RepaymentRow
    {
        public Guid Id { get; init; }

        public Guid FromMemberId { get; init; }

        public Guid ToMemberId { get; init; }

        public long Amount { get; init; }

        public Currency Currency { get; init; }

        public DateOnly Date { get; init; }

        public decimal Rate { get; init; }

        public bool RateManual { get; init; }

        public long GroupAmount { get; init; }

        public string? Note { get; init; }

        public bool Deleted { get; init; }

        public EventRow? Created { get; init; }

        public EventRow? Latest { get; init; }
    }

    private sealed class EventRow
    {
        public Guid Id { get; init; }

        public DateTimeOffset At { get; init; }

        public Guid? ActorUserId { get; init; }

        public Guid? ActorMemberId { get; init; }
    }
}
