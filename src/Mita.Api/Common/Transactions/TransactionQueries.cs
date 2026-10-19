using System.Linq.Expressions;
using Mita.Api.Common.Categories;
using Mita.Api.Common.Money;
using Mita.Api.Common.Wire;
using Mita.Domain.Transactions;

namespace Mita.Api.Common.Transactions;

public static class TransactionQueries
{
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
        transaction.BaseAmount);

    private static Func<Transaction, TransactionDto> Compiled { get; } = ToDto.Compile();

    public static TransactionDto From(Transaction transaction) => Compiled(transaction);
}
