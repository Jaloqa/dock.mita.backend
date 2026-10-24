using Microsoft.EntityFrameworkCore;
using Mita.Domain.Categories;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Common.Groups;

public enum ToConfirmChange
{
    New,
    Changed,
    Removed
}

public sealed record ToConfirmSuggestion(Guid? CategoryId, string? NewCategory, string Title, IReadOnlyList<string> Tags);

public sealed record ToConfirmLine(
    Guid ExpenseId,
    string? Description,
    long Amount,
    Currency Currency,
    long Weight,
    ToConfirmSuggestion Suggestion);

public sealed record ToConfirmItem(
    GroupEntryKind Kind,
    Guid EntryId,
    Guid GroupId,
    string GroupName,
    Currency GroupCurrency,
    Guid MemberId,
    ToConfirmChange Change,
    Guid Version,
    DateTimeOffset At,
    GroupActorDto Actor,
    object? Current,
    object? Previous,
    long Other,
    long OtherWeight,
    IReadOnlyList<ToConfirmLine> Lines,
    Guid? SuggestedAccountId);

public static class ToConfirmItems
{
    public static Task<IReadOnlyList<ToConfirmItem>> PendingAsync(this AppDbContext db, Guid userId, CancellationToken ct) =>
        LoadAsync(db, userId, null, ct);

    public static async Task<ToConfirmItem?> FindAsync(
        this AppDbContext db,
        Guid userId,
        GroupEntryKind kind,
        Guid entryId,
        CancellationToken ct) =>
        (await LoadAsync(db, userId, new EntryKey(kind, entryId), ct)).SingleOrDefault();

    private static async Task<IReadOnlyList<ToConfirmItem>> LoadAsync(
        AppDbContext db,
        Guid userId,
        EntryKey? only,
        CancellationToken ct)
    {
        var memberships = await db.GroupMembers.AsNoTracking()
            .Where(member => member.UserId == userId)
            .Join(
                db.Groups,
                member => member.GroupId,
                group => group.Id,
                (member, group) => new Membership(group.Id, group.Name, group.Currency, member.Id))
            .ToListAsync(ct);
        if (memberships.Count == 0)
        {
            return [];
        }

        var byGroup = memberships.ToDictionary(membership => membership.GroupId);
        var groupIds = byGroup.Keys.ToList();
        var memberIds = memberships.Select(membership => membership.MemberId).ToList();
        var confirmations = await ConfirmationsAsync(db, userId, groupIds, only, ct);
        var expenses = await ExpensesAsync(db, groupIds, memberIds, Decided(confirmations, GroupEntryKind.Expense), only, ct);
        var repayments = await RepaymentsAsync(db, groupIds, memberIds, Decided(confirmations, GroupEntryKind.Repayment), only, ct);

        var pending = expenses
            .Select(expense => Candidate.Of(expense, byGroup[expense.GroupId], confirmations))
            .Concat(repayments.Select(repayment => Candidate.Of(repayment, byGroup[repayment.GroupId], confirmations)))
            .Where(candidate => candidate.IsPending)
            .ToList();
        if (pending.Count == 0)
        {
            return [];
        }

        var newest = await NewestEventsAsync(db, pending, ct);
        var actors = await GroupActors.LoadAsync(db, [.. pending.Select(candidate => candidate.Membership.GroupId).Distinct()], ct);
        var suggestions = await SuggestionsAsync(db, userId, pending, expenses, ct);
        var suggestedAccountIds = pending
            .Select(candidate => candidate.Confirmation?.AccountId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var archivedAccountIds = await db.Accounts.AsNoTracking()
            .Where(account => account.UserId == userId && account.Archived && suggestedAccountIds.Contains(account.Id))
            .Select(account => account.Id)
            .ToListAsync(ct);

        return
        [
            .. pending
                .Select(candidate => candidate.ToItem(
                    newest.GetValueOrDefault(candidate.Key)
                        ?? throw new InvalidOperationException($"The {candidate.Key.Kind} {candidate.Key.EntryId} has no event."),
                    actors.GetValueOrDefault(candidate.Membership.GroupId)
                        ?? throw new InvalidOperationException($"The group {candidate.Membership.GroupId} has no actors."),
                    suggestions,
                    archivedAccountIds))
                .OrderByDescending(item => item.At)
                .ThenByDescending(item => item.EntryId)
        ];
    }

    private static async Task<Dictionary<EntryKey, GroupConfirmation>> ConfirmationsAsync(
        AppDbContext db,
        Guid userId,
        List<Guid> groupIds,
        EntryKey? only,
        CancellationToken ct)
    {
        var confirmations = db.GroupConfirmations.AsNoTracking()
            .Where(confirmation => confirmation.UserId == userId && groupIds.Contains(confirmation.GroupId));
        if (only is { } key)
        {
            confirmations = confirmations.Where(confirmation => confirmation.Kind == key.Kind && confirmation.EntryId == key.EntryId);
        }

        return await confirmations.ToDictionaryAsync(
            confirmation => new EntryKey(confirmation.Kind, confirmation.EntryId),
            ct);
    }

    private static List<Guid> Decided(Dictionary<EntryKey, GroupConfirmation> confirmations, GroupEntryKind kind) =>
        [.. confirmations.Keys.Where(key => key.Kind == kind).Select(key => key.EntryId)];

    private static async Task<List<GroupExpense>> ExpensesAsync(
        AppDbContext db,
        List<Guid> groupIds,
        List<Guid> memberIds,
        List<Guid> decided,
        EntryKey? only,
        CancellationToken ct)
    {
        if (only is { Kind: not GroupEntryKind.Expense })
        {
            return [];
        }

        var expenses = db.GroupExpenses.AsNoTracking()
            .Include(expense => expense.Shares)
            .Where(expense => groupIds.Contains(expense.GroupId)
                && (memberIds.Contains(expense.PayerId) || decided.Contains(expense.Id))
                && (expense.DeletedAt == null || decided.Contains(expense.Id)));
        if (only is { } key)
        {
            expenses = expenses.Where(expense => expense.Id == key.EntryId);
        }

        return await expenses.ToListAsync(ct);
    }

    private static async Task<List<GroupRepayment>> RepaymentsAsync(
        AppDbContext db,
        List<Guid> groupIds,
        List<Guid> memberIds,
        List<Guid> decided,
        EntryKey? only,
        CancellationToken ct)
    {
        if (only is { Kind: not GroupEntryKind.Repayment })
        {
            return [];
        }

        var repayments = db.GroupRepayments.AsNoTracking()
            .Include(repayment => repayment.Covers)
            .Where(repayment => groupIds.Contains(repayment.GroupId)
                && (memberIds.Contains(repayment.FromMemberId)
                    || memberIds.Contains(repayment.ToMemberId)
                    || decided.Contains(repayment.Id))
                && (repayment.DeletedAt == null || decided.Contains(repayment.Id)));
        if (only is { } key)
        {
            repayments = repayments.Where(repayment => repayment.Id == key.EntryId);
        }

        return await repayments.ToListAsync(ct);
    }

    private static async Task<Dictionary<EntryKey, NewestEvent>> NewestEventsAsync(
        AppDbContext db,
        List<Candidate> pending,
        CancellationToken ct)
    {
        var groupIds = pending.Select(candidate => candidate.Membership.GroupId).Distinct().ToList();
        var entryIds = pending.Select(candidate => candidate.Key.EntryId).ToList();
        var newest = await db.GroupEntryEvents.AsNoTracking()
            .Where(entryEvent => groupIds.Contains(entryEvent.GroupId) && entryIds.Contains(entryEvent.EntryId))
            .GroupBy(entryEvent => new { entryEvent.Kind, entryEvent.EntryId })
            .Select(entryEvents => entryEvents
                .OrderByDescending(entryEvent => entryEvent.At)
                .ThenByDescending(entryEvent => entryEvent.Id)
                .Select(entryEvent => new NewestEvent(
                    entryEvent.Kind,
                    entryEvent.EntryId,
                    entryEvent.Id,
                    entryEvent.At,
                    entryEvent.ActorUserId,
                    entryEvent.ActorMemberId))
                .First())
            .ToListAsync(ct);

        return newest.ToDictionary(entryEvent => new EntryKey(entryEvent.Kind, entryEvent.EntryId));
    }

    private static async Task<Suggestions> SuggestionsAsync(
        AppDbContext db,
        Guid userId,
        List<Candidate> pending,
        List<GroupExpense> loaded,
        CancellationToken ct)
    {
        var expenses = loaded.ToDictionary(expense => expense.Id);
        var covered = pending
            .Select(candidate => candidate.Current)
            .OfType<RepaymentTerms>()
            .SelectMany(terms => terms.Covers ?? [])
            .Select(cover => cover.ExpenseId)
            .Where(expenseId => !expenses.ContainsKey(expenseId))
            .Distinct()
            .ToList();
        if (covered.Count > 0)
        {
            foreach (var expense in await db.GroupExpenses.AsNoTracking()
                         .Where(expense => covered.Contains(expense.Id))
                         .ToListAsync(ct))
            {
                expenses[expense.Id] = expense;
            }
        }

        var groupCategoryIds = expenses.Values
            .Where(expense => expense.CategoryId is not null)
            .Select(expense => expense.CategoryId!.Value)
            .Distinct()
            .ToList();
        var groupCategories = await db.GroupCategories.AsNoTracking()
            .Where(category => groupCategoryIds.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, ct);

        var categories = await db.Categories.AsNoTracking()
            .Where(category => category.UserId == userId
                && category.Kind == CategoryKind.Expense
                && category.System != SystemCategory.Transfer)
            .Select(category => new UserCategory(category.Id, category.Name, category.System, category.StandardKey))
            .ToListAsync(ct);
        var transferName = await db.Categories.AsNoTracking()
            .Where(category => category.UserId == userId && category.System == SystemCategory.Transfer)
            .Select(category => category.Name)
            .FirstOrDefaultAsync(ct);

        var confirmationIds = pending
            .Where(candidate => candidate.Confirmation is not null)
            .Select(candidate => candidate.Confirmation!.Id)
            .ToList();
        var earlier = await db.GroupConfirmationTransactions.AsNoTracking()
            .Where(link => confirmationIds.Contains(link.ConfirmationId))
            .Join(
                db.Transactions.Where(transaction => transaction.UserId == userId),
                link => link.TransactionId,
                transaction => transaction.Id,
                (link, transaction) => new EarlierTransaction(
                    link.ConfirmationId,
                    link.ExpenseId,
                    transaction.Id,
                    transaction.CategoryId,
                    transaction.Title,
                    transaction.Tags.Select(tag => new EarlierTag(tag.TagId, tag.Tag.Name)).ToList()))
            .ToListAsync(ct);

        return new Suggestions(
            expenses,
            groupCategories,
            categories,
            transferName,
            earlier
                .GroupBy(transaction => (transaction.ConfirmationId, transaction.ExpenseId))
                .ToDictionary(
                    transactions => transactions.Key,
                    transactions => transactions.OrderBy(transaction => transaction.TransactionId).First()));
    }

    private readonly record struct EntryKey(GroupEntryKind Kind, Guid EntryId);

    private sealed record Membership(Guid GroupId, string GroupName, Currency GroupCurrency, Guid MemberId);

    private sealed record NewestEvent(
        GroupEntryKind Kind,
        Guid EntryId,
        Guid Id,
        DateTimeOffset At,
        Guid? ActorUserId,
        Guid? ActorMemberId);

    private sealed record UserCategory(Guid Id, string Name, SystemCategory? System, string? StandardKey);

    private sealed record EarlierTag(Guid TagId, string Name);

    private sealed record EarlierTransaction(
        Guid ConfirmationId,
        Guid ExpenseId,
        Guid TransactionId,
        Guid CategoryId,
        string Title,
        List<EarlierTag> Tags);

    private sealed class Suggestions(
        Dictionary<Guid, GroupExpense> expenses,
        Dictionary<Guid, string> groupCategories,
        List<UserCategory> categories,
        string? transferName,
        Dictionary<(Guid ConfirmationId, Guid ExpenseId), EarlierTransaction> earlier)
    {
        public ToConfirmLine Line(
            GroupConfirmation? confirmation,
            string groupName,
            Guid expenseId,
            long amount,
            Currency currency,
            long weight)
        {
            var expense = expenses[expenseId];
            return new ToConfirmLine(
                expenseId,
                expense.Description,
                amount,
                currency,
                weight,
                confirmation is not null && earlier.TryGetValue((confirmation.Id, expenseId), out var transaction)
                    ? FromEarlier(expense, transaction)
                    : FromExpense(expense, groupName));
        }

        private ToConfirmSuggestion FromEarlier(GroupExpense expense, EarlierTransaction transaction)
        {
            var (categoryId, newCategory) = categories.Any(category => category.Id == transaction.CategoryId)
                ? (transaction.CategoryId, null)
                : CategoryFor(expense);
            return new ToConfirmSuggestion(
                categoryId,
                newCategory,
                transaction.Title,
                [
                    .. transaction.Tags
                        .OrderBy(tag => tag.Name.ToLowerInvariant(), StringComparer.Ordinal)
                        .ThenBy(tag => tag.TagId)
                        .Select(tag => tag.Name)
                ]);
        }

        private ToConfirmSuggestion FromExpense(GroupExpense expense, string groupName)
        {
            var (categoryId, newCategory) = CategoryFor(expense);
            return new ToConfirmSuggestion(categoryId, newCategory, expense.Description ?? groupName, [.. expense.Tags]);
        }

        private (Guid? CategoryId, string? NewCategory) CategoryFor(GroupExpense expense)
        {
            var other = categories.Find(category => category.System == SystemCategory.Other)?.Id;
            if (expense.StandardCategory is { } key)
            {
                return (categories.Find(category => category.StandardKey == key)?.Id ?? other, null);
            }

            if (expense.CategoryId is not { } groupCategoryId || !groupCategories.TryGetValue(groupCategoryId, out var name))
            {
                return (other, null);
            }

            if (transferName is not null && string.Equals(transferName, name, StringComparison.OrdinalIgnoreCase))
            {
                return (other, null);
            }

            var named = categories.Find(category => string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase));
            return named is null ? (null, name) : (named.Id, null);
        }
    }

    private sealed record Candidate(
        EntryKey Key,
        Membership Membership,
        object? Current,
        GroupConfirmation? Confirmation,
        GroupExpense? Expense,
        GroupRepayment? Repayment)
    {
        public bool IsPending
        {
            get
            {
                var current = GroupTerms.Canonical(Current);
                return current != GroupTerms.Canonical(GroupTerms.Read(Key.Kind, Confirmation?.HandledTerms))
                    && current != GroupTerms.Canonical(GroupTerms.Read(Key.Kind, Confirmation?.ConfirmedTerms));
            }
        }

        public static Candidate Of(
            GroupExpense expense,
            Membership membership,
            Dictionary<EntryKey, GroupConfirmation> confirmations)
        {
            var key = new EntryKey(GroupEntryKind.Expense, expense.Id);
            return new Candidate(
                key,
                membership,
                GroupTerms.For(expense, membership.MemberId),
                confirmations.GetValueOrDefault(key),
                expense,
                null);
        }

        public static Candidate Of(
            GroupRepayment repayment,
            Membership membership,
            Dictionary<EntryKey, GroupConfirmation> confirmations)
        {
            var key = new EntryKey(GroupEntryKind.Repayment, repayment.Id);
            return new Candidate(
                key,
                membership,
                GroupTerms.For(repayment, membership.MemberId),
                confirmations.GetValueOrDefault(key),
                null,
                repayment);
        }

        public ToConfirmItem ToItem(
            NewestEvent newest,
            GroupActors actors,
            Suggestions suggestions,
            List<Guid> archivedAccountIds)
        {
            var change = Current is null
                ? ToConfirmChange.Removed
                : Confirmation?.ConfirmedTerms is null ? ToConfirmChange.New : ToConfirmChange.Changed;
            var (lines, other, otherWeight) = Booking(suggestions);

            return new ToConfirmItem(
                Key.Kind,
                Key.EntryId,
                Membership.GroupId,
                Membership.GroupName,
                Membership.GroupCurrency,
                Membership.MemberId,
                change,
                newest.Id,
                newest.At,
                actors.Describe(newest.ActorUserId, newest.ActorMemberId),
                Current,
                GroupTerms.Read(Key.Kind, Confirmation?.ConfirmedTerms),
                other,
                otherWeight,
                lines,
                SuggestedAccountId(archivedAccountIds));
        }

        private Guid? SuggestedAccountId(List<Guid> archivedAccountIds) =>
            Confirmation is { AccountId: { } accountId } && (Confirmation.ConfirmedTerms is not null || !archivedAccountIds.Contains(accountId))
                ? accountId
                : null;

        private (IReadOnlyList<ToConfirmLine> Lines, long Other, long OtherWeight) Booking(Suggestions suggestions)
        {
            switch (Current)
            {
                case ExpenseTerms { Share: 0 } terms:
                    return ([], terms.Amount, terms.Amount);
                case ExpenseTerms terms:
                    var rest = terms.Amount - terms.Share;
                    var share = suggestions.Line(
                        Confirmation,
                        Membership.GroupName,
                        Key.EntryId,
                        terms.Share,
                        Expense!.Currency,
                        terms.Share);
                    return ([share], rest, rest);
                case RepaymentTerms { Direction: GroupTerms.Paid, GroupAmount: { } groupAmount, Covers: { } covers } terms:
                    var uncovered = groupAmount - covers.Sum(cover => cover.GroupAmount);
                    long[] weights = [.. covers.Select(cover => cover.GroupAmount), uncovered];
                    var parts = MinorAllocation.Allocate(terms.Amount, weights, [.. Enumerable.Range(0, weights.Length)]);
                    return (
                        [
                            .. covers.Select((cover, index) => suggestions.Line(
                                Confirmation,
                                Membership.GroupName,
                                cover.ExpenseId,
                                parts[index],
                                Repayment!.Currency,
                                cover.GroupAmount))
                        ],
                        parts[^1],
                        uncovered);
                case RepaymentTerms terms:
                    return ([], terms.Amount, terms.Amount);
                default:
                    return ([], 0, 0);
            }
        }
    }
}
