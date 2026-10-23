using MediatR;
using Microsoft.EntityFrameworkCore;
using Mita.Api.Authentication;
using Mita.Api.Common;
using Mita.Infrastructure.Persistence;

namespace Mita.Api.Features.Debts.DeleteDebt;

public sealed class DeleteDebtHandler(AppDbContext db, ICurrentUser currentUser)
    : IRequestHandler<DeleteDebtCommand, Result>
{
    public async Task<Result> Handle(DeleteDebtCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var deleted = await db.Debts
            .Where(candidate => candidate.Id == command.Id && candidate.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 0 ? Error.NotFound() : Result.Success();
    }
}
