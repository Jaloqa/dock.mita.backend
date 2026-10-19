using Mita.Api.Common.Transactions;

namespace Mita.Api.Features.Transactions.UpdateTransaction;

public sealed class UpdateTransactionValidator(TimeProvider time) : TransactionInputValidator<UpdateTransactionCommand>(time);
