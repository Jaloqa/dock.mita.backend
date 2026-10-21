using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Categories;
using Mita.Api.Common.Money;
using Mita.Api.Common.Transactions;
using Mita.Api.Common.Wire;
using Mita.Domain.Categories;
using Mita.Domain.Tags;
using Mita.Domain.Transactions;
using Mita.Domain.Users;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Transactions.ListTransactions;

public sealed partial class ListTransactionsHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<ListTransactionsQuery, Result<TransactionPageResponse>>
{
    public async Task<Result<TransactionPageResponse>> Handle(ListTransactionsQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var baseCurrency = await db.Set<UserSettings>().AsNoTracking()
            .Where(settings => settings.UserId == userId)
            .Select(settings => settings.BaseCurrency)
            .SingleAsync(cancellationToken);

        var transactions = Filter(db.Transactions.AsNoTracking().Where(transaction => transaction.UserId == userId), query);

        var total = await transactions.CountAsync(cancellationToken);
        var sum = await transactions.SumAsync(
            transaction => transaction.Kind == CategoryKind.Income ? transaction.BaseAmount : -transaction.BaseAmount,
            cancellationToken);
        var paging = new PageRequest(query.Page, query.PageSize);
        var items = await Order(transactions, query.Sort)
            .Skip(paging.Skip)
            .Take(paging.PageSize)
            .Select(TransactionQueries.ToDto)
            .ToListAsync(cancellationToken);

        return new TransactionPageResponse(items, query.Page, query.PageSize, total, sum, CurrencyWire.ToWire(baseCurrency));
    }

    private IQueryable<Transaction> Filter(IQueryable<Transaction> transactions, ListTransactionsQuery query)
    {
        if (CategoryWire.TryParse(query.Kind, out CategoryKind kind))
        {
            transactions = transactions.Where(transaction => transaction.Kind == kind);
        }

        if (query.CategoryId is not null)
        {
            var categoryId = Guid.Parse(query.CategoryId);
            transactions = transactions.Where(transaction => transaction.CategoryId == categoryId);
        }

        if (query.AccountId is not null)
        {
            var accountId = Guid.Parse(query.AccountId);
            transactions = transactions.Where(transaction => transaction.AccountId == accountId);
        }

        if (TagIdList.TryParse(query.TagIds, out var tagIds) && tagIds.Count > 0)
        {
            var ids = tagIds.ToArray();
            transactions = transactions.Where(transaction =>
                db.TransactionTags.Any(link => link.TransactionId == transaction.Id && ids.Contains(link.TagId)));
        }

        if (IsoDate.TryParse(query.From, out var from))
        {
            transactions = transactions.Where(transaction => transaction.Date >= from);
        }

        if (IsoDate.TryParse(query.To, out var to))
        {
            transactions = transactions.Where(transaction => transaction.Date <= to);
        }

        var search = query.Search?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(search))
        {
            return transactions;
        }

        return Search(transactions, db.Categories, db.TransactionTags, search);
    }

    // These calls are translated to SQL lower(), strpos() and a text cast; the culture and StringComparison overloads the analyzers ask for have no SQL translation.
#pragma warning disable CA1304, CA1305, CA1311, CA1862
    private static IQueryable<Transaction> Search(
        IQueryable<Transaction> transactions,
        IQueryable<Category> categories,
        IQueryable<TransactionTag> transactionTags,
        string search)
    {
        var looksLikeAmount = AmountSearch().IsMatch(search);
        var amountPrefix = search.Replace(',', '.');
        return transactions.Where(transaction =>
            transaction.Title.ToLower().Contains(search)
            || (transaction.Note != null && transaction.Note.ToLower().Contains(search))
            || categories.Any(category => category.Id == transaction.CategoryId && category.Name.ToLower().Contains(search))
            || transactionTags.Any(link => link.TransactionId == transaction.Id && link.Tag.Name.ToLower().Contains(search))
            || (looksLikeAmount
                && ((transaction.Amount / 100).ToString() + "." + (transaction.Amount % 100).ToString().PadLeft(2, '0'))
                    .StartsWith(amountPrefix)));
    }
#pragma warning restore CA1304, CA1305, CA1311, CA1862

    private static IQueryable<Transaction> Order(IQueryable<Transaction> transactions, string? sort)
    {
        var order = TransactionSort.TryParse(sort, out var parsed) ? parsed : TransactionSortOrder.NewestFirst;
        return order switch
        {
            TransactionSortOrder.OldestFirst => transactions
                .OrderBy(transaction => transaction.Date)
                .ThenBy(transaction => transaction.CreatedAt)
                .ThenBy(transaction => transaction.Id),
            TransactionSortOrder.LargestFirst => NewestFirst(transactions.OrderByDescending(transaction => transaction.BaseAmount)),
            TransactionSortOrder.SmallestFirst => NewestFirst(transactions.OrderBy(transaction => transaction.BaseAmount)),
            _ => transactions
                .OrderByDescending(transaction => transaction.Date)
                .ThenByDescending(transaction => transaction.CreatedAt)
                .ThenByDescending(transaction => transaction.Id)
        };
    }

    private static IOrderedQueryable<Transaction> NewestFirst(IOrderedQueryable<Transaction> transactions) => transactions
        .ThenByDescending(transaction => transaction.Date)
        .ThenByDescending(transaction => transaction.CreatedAt)
        .ThenByDescending(transaction => transaction.Id);

    [GeneratedRegex(@"^[0-9]+([.,][0-9]{0,2})?$")]
    private static partial Regex AmountSearch();
}
