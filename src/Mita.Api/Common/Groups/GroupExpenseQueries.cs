using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public static class GroupExpenseQueries
{
    public static async Task<PageResponse<GroupExpenseDto>> PageAsync(
        this AppDbContext db,
        GroupContext group,
        bool deleted,
        PageRequest page,
        CancellationToken ct)
    {
        var expenses = db.GroupExpenses.AsNoTracking().Where(expense => expense.GroupId == group.GroupId);
        var listed = deleted
            ? expenses.Where(expense => expense.DeletedAt != null)
            : expenses.Where(expense => expense.DeletedAt == null);
        var rows = await listed
            .OrderByDescending(expense => expense.Date)
            .ThenByDescending(expense => expense.CreatedAt)
            .ThenByDescending(expense => expense.Id)
            .Project(db, group.GroupId)
            .ToPageAsync(page, ct);

        var actors = await GroupActors.LoadAsync(db, group, ct);
        return new PageResponse<GroupExpenseDto>(
            [.. rows.Items.Select(row => ToDto(row, actors))],
            rows.Page,
            rows.PageSize,
            rows.Total);
    }

    public static async Task<GroupExpenseDto> SingleAsync(
        this AppDbContext db,
        GroupContext group,
        Guid expenseId,
        CancellationToken ct)
    {
        var row = await db.GroupExpenses.AsNoTracking()
            .Where(expense => expense.Id == expenseId && expense.GroupId == group.GroupId)
            .Project(db, group.GroupId)
            .SingleAsync(ct);

        return ToDto(row, await GroupActors.LoadAsync(db, group, ct));
    }

    private static IQueryable<ExpenseRow> Project(this IQueryable<GroupExpense> expenses, AppDbContext db, Guid groupId)
    {
        var events = db.GroupEntryEvents
            .Where(entryEvent => entryEvent.GroupId == groupId && entryEvent.Kind == GroupEntryKind.Expense);

        return expenses.Select(expense => new ExpenseRow
        {
            Id = expense.Id,
            PayerId = expense.PayerId,
            Amount = expense.Amount,
            Currency = expense.Currency,
            Date = expense.Date,
            StandardCategory = expense.StandardCategory,
            CategoryId = expense.CategoryId,
            Tags = expense.Tags,
            Description = expense.Description,
            Rate = expense.Rate,
            RateManual = expense.RateManual,
            GroupAmount = expense.GroupAmount,
            Split = expense.Split,
            Deleted = expense.DeletedAt != null,
            Shares = expense.Shares
                .Join(
                    db.GroupMembers,
                    share => share.MemberId,
                    member => member.Id,
                    (share, member) => new { share.MemberId, share.Amount, share.GroupAmount, member.Position })
                .OrderBy(share => share.Position)
                .Select(share => new GroupShareDto(share.MemberId, share.Amount, share.GroupAmount))
                .ToList(),
            Created = events
                .Where(entryEvent => entryEvent.EntryId == expense.Id)
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
                .Where(entryEvent => entryEvent.EntryId == expense.Id)
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

    private static GroupExpenseDto ToDto(ExpenseRow row, GroupActors actors)
    {
        var created = row.Created ?? throw new InvalidOperationException("Every expense has a creation event.");
        var updated = row.Latest is { } latest && latest.Id != created.Id ? latest : null;

        return new GroupExpenseDto(
            row.Id,
            row.PayerId,
            row.Amount,
            CurrencyWire.ToWire(row.Currency),
            IsoDate.ToWire(row.Date),
            GroupWire.ToWireCategory(row.StandardCategory, row.CategoryId),
            row.Tags,
            row.Description,
            (double)row.Rate,
            row.RateManual,
            row.GroupAmount,
            GroupWire.ToWire(row.Split),
            row.Shares,
            row.Deleted,
            actors.Describe(created.ActorUserId, created.ActorMemberId),
            created.At,
            updated is null ? null : actors.Describe(updated.ActorUserId, updated.ActorMemberId),
            updated?.At);
    }

    private sealed class ExpenseRow
    {
        public Guid Id { get; init; }

        public Guid PayerId { get; init; }

        public long Amount { get; init; }

        public Currency Currency { get; init; }

        public DateOnly Date { get; init; }

        public string? StandardCategory { get; init; }

        public Guid? CategoryId { get; init; }

        public IReadOnlyList<string> Tags { get; init; } = [];

        public string? Description { get; init; }

        public decimal Rate { get; init; }

        public bool RateManual { get; init; }

        public long GroupAmount { get; init; }

        public GroupSplitMode Split { get; init; }

        public bool Deleted { get; init; }

        public List<GroupShareDto> Shares { get; init; } = [];

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
