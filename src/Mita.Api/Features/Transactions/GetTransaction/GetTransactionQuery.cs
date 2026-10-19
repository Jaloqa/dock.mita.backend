using MediatR;
using Mita.Api.Common;
using Mita.Api.Common.Transactions;

namespace Mita.Api.Features.Transactions.GetTransaction;

public sealed record GetTransactionQuery(Guid Id) : IRequest<Result<TransactionDto>>;
