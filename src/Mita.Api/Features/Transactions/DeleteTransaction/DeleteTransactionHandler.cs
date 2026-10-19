using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Transactions.DeleteTransaction;

public sealed class DeleteTransactionHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteTransactionCommand, Result>
{
    public async Task<Result> Handle(DeleteTransactionCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var deleted = await db.Transactions
            .Where(candidate => candidate.Id == command.Id && candidate.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? Error.NotFound() : Result.Success();
    }
}
