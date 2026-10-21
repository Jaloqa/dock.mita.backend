using System.Linq.Expressions;
using Mita.Api.Common.Categories;
using Mita.Api.Common.Money;
using Mita.Api.Common.Tags;
using Mita.Api.Common.Wire;
using Mita.Domain.Transactions;

namespace Mita.Api.Common.Transactions;

public static class TransactionQueries
{
#pragma warning disable CA1304, CA1305, CA1311, CA1862
    public static Expression<Func<Transaction, TransactionDto>> ToDto { get; } = transaction => new TransactionDto(
        transaction.Id,
        CategoryWire.ToWire(transaction.Kind),
        IsoDate.ToWire(transaction.Date),
        transaction.Title,
        transaction.Note,
        transaction.CategoryId,
        transaction.AccountId,
        transaction.Amount,
        CurrencyWire.ToWire(transaction.Currency),
        transaction.AccountAmount,
        (double)transaction.Rate,
        transaction.BaseAmount,
        transaction.Tags
            .OrderBy(link => link.Tag.Name.ToLower())
            .ThenBy(link => link.TagId)
            .Select(link => new TransactionTagDto(link.TagId, link.Tag.Name))
            .ToList());
#pragma warning restore CA1304, CA1305, CA1311, CA1862
}
