using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Api.Common.Transactions;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Transactions.GetTransaction;

public sealed class GetTransactionHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetTransactionQuery, Result<TransactionDto>>
{
    public async Task<Result<TransactionDto>> Handle(GetTransactionQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var transaction = await db.Transactions.AsNoTracking()
            .Where(candidate => candidate.Id == query.Id && candidate.UserId == userId)
            .Select(TransactionQueries.ToDto)
            .SingleOrDefaultAsync(cancellationToken);

        return transaction is null ? Error.NotFound() : transaction;
    }
}
