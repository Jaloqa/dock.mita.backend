using Mita.Api.Common.Transactions;

namespace Mita.Api.Features.Transactions.CreateTransaction;

public sealed class CreateTransactionValidator(TimeProvider time) : TransactionInputValidator<CreateTransactionCommand>(time);
