using Microsoft.EntityFrameworkCore;
using Mita.Api.Common.Money;
using Mita.Api.Common.Rates;
using Mita.Api.Common.Settings;
using Mita.Api.Common.Tags;
using Mita.Api.Common.Time;
using Mita.Api.Common.Transactions;
using Mita.Api.Common.Wire;
using Mita.Domain.Accounts;
using Mita.Domain.Categories;
using Mita.Domain.Groups;
using Mita.Domain.Money;
using Mita.Domain.Transactions;
using Mita.Infrastructure.Persistence;
using Npgsql;

namespace Mita.Api.Common.Groups;

public sealed record ParsedConfirmLine(Guid? CategoryId, string? NewCategory, string Title, IReadOnlyList<string> Tags);

public sealed record ParsedConfirmation(
    Guid Version,
    Guid? AccountId,
    decimal? ManualRate,
    IReadOnlyList<ParsedConfirmLine>? Lines);

public sealed record LockedItem(ToConfirmItem Item, GroupConfirmation Confirmation);

public sealed class GroupRecordWriter(AppDbContext db, GroupAccess access, TimeProvider time)
{
    private const string Required = "validation.required";
    private const string InvalidValue = "validation.invalidValue";
    private const string PositiveAmount = "validation.positiveAmount";
    private const string CategoryNameTaken = "validation.categoryNameTaken";
    private const string LinesField = "lines";
    private const string RateField = "rate";
    private const string ConfirmationIndex = "IX_group_confirmations_UserId_Kind_EntryId";
    private const string CategoryNameIndex = "IX_categories_user_kind_name";

    public async Task<Result<LockedItem>> LockAsync(
        Guid userId,
        GroupEntryKind kind,
        Guid entryId,
        Guid version,
        CancellationToken ct)
    {
        if (await EntryGroupIdAsync(kind, entryId, ct) is not { } groupId
            || await access.OpenAsync(GroupScope.User(groupId), GroupLock.Share, ct) is not { MyMemberId: not null })
        {
            return Error.NotFound();
        }

        // Expense writes take the group only FOR SHARE, so the entry row lock is what makes them wait until the
        // terms and the version below are read and the decision is committed.
        await LockEntryAsync(kind, entryId, ct);
        var confirmation = await LockConfirmationAsync(userId, kind, entryId, ct);
        var item = await ToConfirmItems.FindAsync(db, userId, kind, entryId, ct);
        if (item is null)
        {
            return Error.NotFound();
        }

        if (item.Version != version)
        {
            return GroupErrors.ItemChanged;
        }

        if (confirmation is not null)
        {
            return new LockedItem(item, confirmation);
        }

        confirmation = GroupConfirmation.Create(userId, item.GroupId, kind, entryId);
        confirmation.Decide(null, time.GetUtcNow());
        db.GroupConfirmations.Add(confirmation);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, ConfirmationIndex))
        {
            return GroupErrors.ItemChanged;
        }

        return new LockedItem(item, confirmation);
    }

    public async Task<Result> BookAsync(
        Guid userId,
        ToConfirmItem item,
        ParsedConfirmation input,
        GroupConfirmation confirmation,
        CancellationToken ct)
    {
        var entry = Entry.Of(item.Current);
        var errors = new Dictionary<string, string[]>();
        if (input.AccountId is null)
        {
            errors[TransactionReferenceRules.AccountField] = [Required];
        }

        var lines = input.Lines is { } given && given.Count == item.Lines.Count ? given : null;
        if (lines is null)
        {
            errors[LinesField] = [InvalidValue];
        }

        var (baseCurrency, _) = await db.LockSettingsAsync(userId, ct);
        var account = input.AccountId is { } accountId ? await LockAccountAsync(userId, accountId, ct) : null;
        if (input.AccountId is not null
            && TransactionReferenceRules.AccountError(account?.Archived, KeepsAccount(confirmation, account)) is { } accountError)
        {
            errors[TransactionReferenceRules.AccountField] = [accountError];
        }

        var categories = lines is null ? null : await LineCategoriesAsync(userId, lines, errors, ct);
        if (errors.Count > 0 || account is null || lines is null || categories is null)
        {
            return Error.Validation(errors);
        }

        var plnValues = await db.FreshPlnValuesAsync(entry.Date, UtcCalendar.Today(time), ct);
        var price = TransactionPrice.Compute(entry.Amount, entry.Currency, account.Currency, baseCurrency, input.ManualRate, plnValues);
        if (price is null)
        {
            return Error.Unavailable("rates.unavailable");
        }

        if (price.AccountAmount <= 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { [RateField] = [PositiveAmount] });
        }

        if (price.AccountAmount > MinorUnits.MaxAmount)
        {
            return Error.Validation(new Dictionary<string, string[]> { [RateField] = [InvalidValue] });
        }

        await RemoveAsync(confirmation, ct);

        long[] weights = [.. item.Lines.Select(line => line.Weight), item.OtherWeight];
        var parts = MinorAllocation.Allocate(price.AccountAmount, weights, [.. Enumerable.Range(0, weights.Length)]);
        var leg = parts[^1];
        for (var index = 0; index < item.Lines.Count; index++)
        {
            var line = item.Lines[index];
            var part = parts[index];
            if (line.Amount <= 0 || part <= 0)
            {
                leg += part;
                continue;
            }

            var baseAmount = TransactionPrice.PriceInBase(line.Amount, line.Currency, part, account.Currency, baseCurrency, plnValues)
                ?? throw new InvalidOperationException("The base rate of the entry was resolved, so every line has one.");
            var transaction = Transaction.Create(
                userId,
                new TransactionDetails(
                    CategoryKind.Expense,
                    entry.Date,
                    lines[index].Title,
                    null,
                    lines[index].CategoryId ?? categories.Resolve(userId, lines[index].NewCategory!),
                    account.Id,
                    line.Amount,
                    line.Currency,
                    price.Rate,
                    part,
                    baseAmount));
            db.Transactions.Add(transaction);
            await db.ReplaceAsync(transaction.Id, await db.UpsertAsync(userId, lines[index].Tags, ct), ct);
            db.GroupConfirmationTransactions.Add(GroupConfirmationTransaction.Create(transaction.Id, confirmation.Id, line.ExpenseId));
        }

        if (leg != 0)
        {
            db.GroupAccountLegs.Add(GroupAccountLeg.Create(userId, confirmation.Id, account.Id, entry.Date, entry.Sign * leg));
        }

        confirmation.Confirm(GroupTerms.Canonical(item.Current), account.Id, time.GetUtcNow());
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, CategoryNameIndex))
        {
            return GroupErrors.ItemChanged;
        }

        return Result.Success();
    }

    public async Task RemoveAsync(GroupConfirmation confirmation, CancellationToken ct)
    {
        var booked = db.GroupConfirmationTransactions
            .Where(link => link.ConfirmationId == confirmation.Id)
            .Select(link => link.TransactionId);
        await db.Transactions
            .Where(transaction => transaction.UserId == confirmation.UserId && booked.Contains(transaction.Id))
            .ExecuteDeleteAsync(ct);
        await db.GroupAccountLegs
            .Where(leg => leg.ConfirmationId == confirmation.Id)
            .ExecuteDeleteAsync(ct);
    }

    private Task<Guid?> EntryGroupIdAsync(GroupEntryKind kind, Guid entryId, CancellationToken ct) => kind switch
    {
        GroupEntryKind.Expense => db.GroupExpenses.AsNoTracking()
            .Where(expense => expense.Id == entryId)
            .Select(expense => (Guid?)expense.GroupId)
            .SingleOrDefaultAsync(ct),
        GroupEntryKind.Repayment => db.GroupRepayments.AsNoTracking()
            .Where(repayment => repayment.Id == entryId)
            .Select(repayment => (Guid?)repayment.GroupId)
            .SingleOrDefaultAsync(ct),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private Task<List<Guid>> LockEntryAsync(GroupEntryKind kind, Guid entryId, CancellationToken ct) => kind switch
    {
        GroupEntryKind.Expense => db.Database
            .SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM group_expenses WHERE "Id" = {entryId} FOR SHARE""")
            .ToListAsync(ct),
        GroupEntryKind.Repayment => db.Database
            .SqlQuery<Guid>($"""SELECT "Id" AS "Value" FROM group_repayments WHERE "Id" = {entryId} FOR SHARE""")
            .ToListAsync(ct),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private async Task<GroupConfirmation?> LockConfirmationAsync(
        Guid userId,
        GroupEntryKind kind,
        Guid entryId,
        CancellationToken ct)
    {
        var storedKind = kind.ToString();
        var rows = await db.GroupConfirmations
            .FromSql($"""
                SELECT * FROM group_confirmations
                WHERE "UserId" = {userId} AND "Kind" = {storedKind} AND "EntryId" = {entryId}
                FOR UPDATE
                """)
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    private async Task<Account?> LockAccountAsync(Guid userId, Guid accountId, CancellationToken ct)
    {
        var rows = await db.Accounts
            .FromSql($"""SELECT * FROM accounts WHERE "Id" = {accountId} AND "UserId" = {userId} FOR SHARE""")
            .AsNoTracking()
            .ToListAsync(ct);
        return rows.SingleOrDefault();
    }

    private async Task<LineCategories> LineCategoriesAsync(
        Guid userId,
        IReadOnlyList<ParsedConfirmLine> lines,
        Dictionary<string, string[]> errors,
        CancellationToken ct)
    {
        var ids = lines.Select(line => line.CategoryId).OfType<Guid>().Distinct().ToArray();
        var chosen = ids.Length == 0
            ? new Dictionary<Guid, Category>()
            : await db.Categories
                .FromSql($"""SELECT * FROM categories WHERE "UserId" = {userId} AND "Id" = ANY({ids}) FOR SHARE""")
                .AsNoTracking()
                .ToDictionaryAsync(category => category.Id, ct);

        var named = new Dictionary<string, Category?>(StringComparer.OrdinalIgnoreCase);
        var expenseKind = nameof(CategoryKind.Expense);
        foreach (var name in lines.Select(line => line.NewCategory).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var matches = await db.Categories
                .FromSql($"""
                    SELECT * FROM categories
                    WHERE "UserId" = {userId} AND "Kind" = {expenseKind} AND lower("Name") = lower({name})
                    FOR SHARE
                    """)
                .AsNoTracking()
                .ToListAsync(ct);
            named[name] = matches.SingleOrDefault();
        }

        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].CategoryId is { } categoryId)
            {
                if (chosen.GetValueOrDefault(categoryId) is not { Kind: CategoryKind.Expense, System: not SystemCategory.Transfer })
                {
                    errors[$"{LinesField}.{index}.categoryId"] = [TransactionReferenceRules.UnknownCategory];
                }
            }
            else if (lines[index].NewCategory is { } name && named[name] is { System: SystemCategory.Transfer })
            {
                errors[$"{LinesField}.{index}.newCategory"] = [CategoryNameTaken];
            }
        }

        return new LineCategories(db, named);
    }

    private static bool KeepsAccount(GroupConfirmation confirmation, Account? account) =>
        account is not null && confirmation.ConfirmedTerms is not null && confirmation.AccountId == account.Id;

    private static bool IsUniqueViolation(DbUpdateException exception, string constraint) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && postgres.ConstraintName == constraint;

    private sealed class LineCategories(AppDbContext db, Dictionary<string, Category?> named)
    {
        public Guid Resolve(Guid userId, string name)
        {
            if (named[name] is { } existing)
            {
                return existing.Id;
            }

            var created = Category.Create(userId, name, CategoryKind.Expense, CategoryColor.Chart4);
            db.Categories.Add(created);
            named[name] = created;
            return created.Id;
        }
    }

    private sealed record Entry(long Amount, Currency Currency, DateOnly Date, int Sign)
    {
        public static Entry Of(object? terms) => terms switch
        {
            ExpenseTerms expense => Create(expense.Amount, expense.Currency, expense.Date, -1),
            RepaymentTerms { Direction: GroupTerms.Received } received => Create(received.Amount, received.Currency, received.Date, 1),
            RepaymentTerms paid => Create(paid.Amount, paid.Currency, paid.Date, -1),
            _ => throw new InvalidOperationException("Only an item with current terms books records.")
        };

        private static Entry Create(long amount, string currency, string date, int sign) =>
            CurrencyWire.TryParse(currency, out var parsedCurrency) && IsoDate.TryParse(date, out var parsedDate)
                ? new Entry(amount, parsedCurrency, parsedDate, sign)
                : throw new InvalidOperationException("Terms carry a known currency and an ISO date.");
    }
}
